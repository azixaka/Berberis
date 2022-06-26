﻿using Microsoft.Extensions.Logging;
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

    private bool _tracingEnabled;

    public string TracingChannel { get; } = "$message.traces";

    public bool TracingEnabled
    {
        get => _tracingEnabled;
        set
        {
            if (value && _tracingEnabled != value)
            {
                _ =GetOrAddChannel<MessageTrace>(TracingChannel, typeof(MessageTrace));
                _tracingEnabled = value;
            }
        }
    }

    public CrossBar(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CrossBar>();
    }

    public ValueTask Publish<TBody>(string channel, TBody body) => Publish(channel, body, 0, null, false, null);
    public ValueTask Publish<TBody>(string channel, TBody body, string from) => Publish(channel, body, 0, null, false, from);
    public ValueTask Publish<TBody>(string channel, TBody body, long correlationId) => Publish(channel, body, correlationId, null, false, null);
    public ValueTask Publish<TBody>(string channel, TBody body, string key, bool store) => Publish(channel, body, 0, key, store, null);

    public ValueTask Publish<TBody>(string channelName, TBody body, long correlationId, string? key, bool store, string? from)
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
        if (pubType == channel.BodyType)
        {
            channel.Statistics.IncNumOfMessages();

            var id = channel.NextMessageId();
            var msg = new Message<TBody>(id, timestamp.ToBinary(), correlationId, key, ticks, from, body);

            if (TracingEnabled)
            {
                PublishSystem(TracingChannel,
                                new MessageTrace
                                {
                                    MessageId = id,
                                    MessageKey = key,
                                    CorrelationId = correlationId,
                                    From = from,
                                    Channel = channelName,
                                    Ticks = ticks
                                });
            }

            if (store)
            {
                var messageStore = channel.GetMessageStore<TBody>();
                if (messageStore != null)
                {
                    messageStore.Update(msg);
                }
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
            _logger.LogWarning("Failed to publish [{pubType}] message type on channel [{channel}] registered with type [{chanType}]",
                pubType.Name, channelName, channel.BodyType.Name);

            return ValueTask.FromException(new InvalidOperationException($"Can't publish [{pubType.Name}] message type on channel [{channelName}] registered with type [{channel.BodyType.Name}]"));
        }

        return ValueTask.CompletedTask;
    }

    internal ValueTask PublishSystem<TBody>(string channelName, TBody body)
    {
        var channel = GetSystemChannel(channelName);
        if (channel != null)
        {
            channel.Statistics.IncNumOfMessages();

            var msg = new Message<TBody>(0, 0, 0, null, 0, null, body);

            foreach (var (_, subObj) in channel.Subscriptions)
            {
                if (subObj is Subscription<TBody> subscription)
                {
                    subscription.TryWrite(msg);
                }
            }

            channel.Statistics.DecNumOfMessages();
        }

        return ValueTask.CompletedTask;
    }

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler)
        => Subscribe(channel, handler, false, SlowConsumerStrategy.SkipUpdates, null, -1);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState)
        => Subscribe(channel, handler, fetchState, SlowConsumerStrategy.SkipUpdates, null, -1);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, int conflationIntervalMilliseconds)
        => Subscribe(channel, handler, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationIntervalMilliseconds);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity,
                                          int conflationIntervalMilliseconds)
    {
        EnsureNotDisposed();

        var sysChannel = GetSystemChannel(channelName);

        var subType = typeof(TBody);

        var channel = sysChannel ?? GetOrAddChannel<TBody>(channelName, subType);

        // if channel was already there and its type matches the type passed with this call
        if (channel.BodyType == subType)
        {
            Func<IEnumerable<Message<TBody>>> stateFactory = null;

            if (sysChannel == null && fetchState)
            {
                var messageStore = channel.GetMessageStore<TBody>();
                if (messageStore != null)
                {
                    stateFactory = messageStore.GetState;
                }
            }

            //create, register and return subscription
            long id = Interlocked.Increment(ref _globalSubId);

            var subscription = sysChannel == null ? new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                       id, channelName, bufferCapacity, conflationIntervalMilliseconds, slowConsumerStrategy, handler,
                                                       () => Unsubscribe(channelName, id),
                                                       stateFactory, this, false)

                                                   : new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                       id, channelName, 1000, -1, SlowConsumerStrategy.SkipUpdates, handler,
                                                       () => Unsubscribe(channelName, id),
                                                       stateFactory, this, true);

            if (channel.Subscriptions.TryAdd(id, subscription))
            {
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

    public bool TryDeleteMessage<TBody>(string channelName, string key, out Message<TBody> message)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            var messageStore = channel.Value.GetMessageStore<TBody>();

            if (messageStore != null)
            {
                return messageStore.TryDelete(key, out message);
            }
        }

        message = Message<TBody>.Default;
        return false;
    }

    public bool ResetStore<TBody>(string channelName)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            var messageStore = channel.Value.GetMessageStore<TBody>();

            if (messageStore != null)
            {
                messageStore.Reset();
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<ChannelInfo> GetChannels()
    {
        EnsureNotDisposed();

        return _channels
                    .Where(kvp => !kvp.Key.StartsWith("$"))
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

    private Channel? GetSystemChannel(string channel)
    {
        if (channel.StartsWith("$") && _channels.TryGetValue(channel, out var channelProxy))
        {
            return channelProxy.Value;
        }

        return null;
    }

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
