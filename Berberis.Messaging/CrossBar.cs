using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Berberis.Messaging;

public sealed partial class CrossBar : ICrossBar, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CrossBar> _logger;
    private long _globalSubId;
    private long _globalCorrelationId;
    private readonly ConcurrentDictionary<string, Lazy<Channel>> _channels = new();
    private int _isDisposed;

    private readonly FailedSubscriptionException _failedSubscriptionException = new();

    public CrossBar(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CrossBar>();
    }

    public ValueTask Publish<TBody>(string channelName, TBody body, long correlationId, string? key, bool store)
    {
        var ticks = StatsTracker.GetTicks();
        var timestamp = DateTime.UnixEpoch;

        EnsureNotDisposed();

        if (store && string.IsNullOrEmpty(key))
        {
            throw new FailedPublishException($"Stored message must have key specified. Channel: {channelName}");
        }

        var pubType = typeof(TBody);

        var channel = GetOrAddChannel<TBody>(channelName, pubType);

        // if channel was already there and its type matches the type passed with this call
        if (channel.BodyType == pubType)
        {
            channel.Statistics.IncNumOfMessages();

            var msg = new Message<TBody>(channel.NextMessageId(),
                                         timestamp.ToBinary(), correlationId,
                                         key, ticks, body);

            if (store)
            {
                var messageStore = channel.GetMessageStore<TBody>();
                messageStore.Update(msg);
            }

            // walk through all the subscriptions on this channel...
            foreach (var (_, subObj) in channel.Subscriptions)
            {
                if (subObj is Subscription<TBody> subscription) // Subscribe method ensures this will always cast successfully
                {
                    // and send the body wrapped into a Message envelope with some metadata
                    if (subscription.TryWrite(msg))
                    {
                        LogMessageSent(msg.Id, msg.CorrelationId, msg.Key ?? string.Empty, subscription.Id, channelName);
                    }
                    else
                    {
                        switch (subscription.SlowConsumerStrategy)
                        {
                            case SlowConsumerStrategy.SkipUpdates:
                                _logger.LogWarning("Subscription [{subId}] SKIPPED message [{msgId}] | correlation [{corId}] on channel [{channel}]",
                                    subscription.Id, msg.Id, msg.CorrelationId, channelName);
                                break;

                            case SlowConsumerStrategy.FailSubscription:
                                _ = subscription.TryFail(_failedSubscriptionException);

                                _logger.LogWarning("Subscription [{subId}] FAILED to receive message [{msgId}] | correlation [{corId}] on channel [{channel}]",
                                   subscription.Id, msg.Id, msg.CorrelationId, channelName);
                                break;
                        }
                    }
                }
            }

            channel.Statistics.DecNumOfMessages();
        }
        else // if channel exists but its type is different to the TBody being published here...
        {
            _logger.LogWarning("Failed to publish [{pubType}] message type on channel [{channel}] registered with type [{pubType}]",
                pubType.Name, channelName, channel.BodyType.Name);

            return ValueTask.FromException(new InvalidOperationException($"Can't publish [{pubType.Name}] message type on channel [{channelName}] registered with type [{channel.BodyType.Name}]"));
        }

        return ValueTask.CompletedTask;
    }

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity)
    {
        EnsureNotDisposed();

        var subType = typeof(TBody);

        var channel = GetOrAddChannel<TBody>(channelName, subType);

        // if channel was already there and its type matches the type passed with this call
        if (channel.BodyType == subType)
        {
            //create, register and return subscription
            long id = Interlocked.Increment(ref _globalSubId);

            Func<IEnumerable<Message<TBody>>> stateFactory = null;

            if (fetchState)
            {
                var messageStore = channel.GetMessageStore<TBody>();
                stateFactory = messageStore.GetState;
            }

            var subscription = new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                       id, bufferCapacity, slowConsumerStrategy, handler,
                                                       () => Unsubscribe(channelName, id),
                                                       stateFactory);

            if (channel.Subscriptions.TryAdd(id, subscription))
            {
                //TODO: keep track of the last message seqid / timestamp sent on this subscription to prevent sending new update before or while sending the state!
                //TODO: add DELETE key/RESET state

                _logger.LogInformation("Subscribed [{id}] on channel [{channel}]", id, channelName);
            }
            else { } // can't happen due to atomic id increments above

            // the loop can get cancelled by disopsing subscription, see Subscription's ReadLoop/Dispose pair
            // so Subscription can get returned running if needed by : _ = subscription.RunReadLoopAsync();

            return subscription;
        }
        else // not the type Subscribe caller was expecting
        {
            _logger.LogWarning("Failed to subscribe on channel [{channel}] with type [{subType}] as it's already registered with type [{regType}]", channelName, subType.Name, channel.BodyType.Name);
            throw new InvalidOperationException($"Can't subscribe on channel [{channelName}] with type [{subType.Name}] as it's already registered with type [{channel.BodyType.Name}]");
        }
    }

    public IReadOnlyList<ChannelInfo> GetChannels()
    {
        EnsureNotDisposed();

        return _channels
                    .Select(kvp => new ChannelInfo { Name = kvp.Key, BodyType = kvp.Value.Value.BodyType })
                    .ToList();
    }

    public IReadOnlyCollection<SubscriptionInfo> GetChannelSubscriptions(string channelName)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            return channel.Value.Subscriptions
                .Select(kvp =>
                {
                    return new SubscriptionInfo
                    {
                        Id = kvp.Value.Id,
                        Statistics = kvp.Value.Statistics
                    };
                })
                .ToList();
        }

        return null;
    }

    public long GetNextCorrelationId() => Interlocked.Increment(ref _globalCorrelationId);

    private Channel GetOrAddChannel<TBody>(string channel, Type bodyType)
    {
        //ConcurrentDictionary's create factory can be called multiple times but only one result wins and gets added as a value for that key
        //By wrapping it with Lazy, we ensure we materialise it only once (.Value)
        return _channels.GetOrAdd(channel,
            c => new Lazy<Channel>(() =>
            {
                _logger.LogInformation("Channel [{channel}] for type [{bodyType}] is created.", c, bodyType);
                return new Channel
                {
                    BodyType = bodyType
                };
            }))
            .Value;
    }

    private void Unsubscribe(string channelName, long id)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            if (channel.Value.Subscriptions.TryRemove(id, out _))
            {
                _logger.LogInformation("Unsubscribed [{id}] from channel [{channel}]", id, channelName);
            }
            else { } // can't happen as this is called from the Subscription.Dispose which can be called just once and it gets that subscription's id passed
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            foreach (var channel in _channels)
            {
                foreach (var (_, sub) in channel.Value.Value.Subscriptions)
                {
                    sub.TryDispose();
                }
            }

            _channels.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureNotDisposed()
    {
        if (Volatile.Read(ref _isDisposed) == 1)
            throw new ObjectDisposedException(nameof(CrossBar));
    }

    [LoggerMessage(0, LogLevel.Trace, "Sent message [{messageId}] | correlation [{corId}] | key [{key}] to subscription [{subscriptionId}] on channel [{channel}]")]
    partial void LogMessageSent(long messageId, long corId, string key, long subscriptionId, string channel);
}
