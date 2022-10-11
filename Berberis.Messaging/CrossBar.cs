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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, ISubscription>> _wildcardSubscriptions = new();
    private int _isDisposed;

    private readonly FailedSubscriptionException _failedSubscriptionException = new();

    private bool _tracingEnabled;

    public string TracingChannel { get; } = "$message.traces";

    public bool MessageTracingEnabled
    {
        get => _tracingEnabled;
        set
        {
            if (value && _tracingEnabled != value)
            {
                _ = GetOrAddChannel(TracingChannel, typeof(MessageTrace));
                _tracingEnabled = value;
            }
        }
    }

    public bool PublishLoggingEnabled { get; set; }

    public CrossBar(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CrossBar>();
    }

    public ValueTask Publish<TBody>(string channelName, TBody body, long correlationId, string? key, bool store, string? from)
    {
        var ticks = StatsTracker.GetTicks();
        var timestamp = DateTime.UtcNow;

        EnsureNotDisposed();

        if (store && string.IsNullOrEmpty(key))
        {
            throw new FailedPublishException($"Stored message must have key specified. Channel: {channelName}");
        }

        var pubType = typeof(TBody);

        var channel = GetOrAddChannel(channelName, pubType);

        // if channel was already there and its type matches the type passed with this call
        if (pubType == channel.BodyType)
        {
            var id = channel.NextMessageId();
            var msg = new Message<TBody>(id, timestamp.ToBinary(), correlationId, key, ticks, from, body);

            if (MessageTracingEnabled)
            {
                PublishSystem(TracingChannel,
                                new MessageTrace
                                {
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

            channel.LastPublishedAt = timestamp;
            channel.LastPublishedBy = from;

            // walk through all the subscriptions on this channel...
            foreach (var (_, subObj) in channel.Subscriptions)
            {
                if (subObj is Subscription<TBody> subscription) // Subscribe method ensures this will always cast successfully
                {
                    // and send the body wrapped into a Message envelope with some metadata
                    if (subscription.TryWrite(msg))
                    {
                        if (PublishLoggingEnabled)
                        {
                            LogMessageSent(msg.Id, msg.CorrelationId, msg.Key ?? string.Empty, subscription.Name, channelName);
                        }
                    }
                    else
                    {
                        switch (subscription.SlowConsumerStrategy)
                        {
                            case SlowConsumerStrategy.SkipUpdates:
                                _logger.LogWarning("Subscription [{sub}] SKIPPED message [{msgId}] | correlation [{corId}] on channel [{channel}]",
                                    subscription.Name, msg.Id, msg.CorrelationId, channelName);
                                break;

                            case SlowConsumerStrategy.FailSubscription:
                                _ = subscription.TryFail(_failedSubscriptionException);

                                _logger.LogWarning("Subscription [{sub}] FAILED to receive message [{msgId}] | correlation [{corId}] on channel [{channel}]",
                                   subscription.Name, msg.Id, msg.CorrelationId, channelName);
                                break;
                        }
                    }
                }
            }

            channel.Statistics.IncNumOfPublishedMessages();
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
            var msg = new Message<TBody>(0, 0, 0, null, 0, null, body);

            foreach (var (_, subObj) in channel.Subscriptions)
            {
                if (subObj is Subscription<TBody> subscription)
                {
                    subscription.TryWrite(msg);
                }
            }

            channel.Statistics.IncNumOfPublishedMessages();
        }

        return ValueTask.CompletedTask;
    }

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          string? subscriptionName,
                                          bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity,
                                          TimeSpan conflationInterval,
                                          CancellationToken token = default)
    {
        EnsureNotDisposed();

        if (IsSystemChannel(channelName))
        {
            return SubscribeSystem(channelName, handler, subscriptionName, token);
        }

        if (IsWildcardSubscription(channelName))
        {
            return SubscribeWildcard(channelName, handler, subscriptionName, fetchState, slowConsumerStrategy, bufferCapacity, conflationInterval, token);
        }

        var subType = typeof(TBody);

        var channel = GetOrAddChannel(channelName, subType);

        // if channel was already there and its type matches the type passed with this call
        if (channel.BodyType == subType)
        {
            Func<IEnumerable<Message<TBody>>>? stateFactory = null;

            if (fetchState)
            {
                var messageStore = channel.GetMessageStore<TBody>();
                if (messageStore != null)
                {
                    stateFactory = messageStore.GetState;
                }
            }

            //create, register and return subscription
            long id = Interlocked.Increment(ref _globalSubId);

            var subscription = new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                       id, subscriptionName, channelName, bufferCapacity, conflationInterval, slowConsumerStrategy, handler,
                                                       () => Unsubscribe(channelName, id),
                                                       stateFactory != null ? new[] { stateFactory } : null, this, false, false);

            if (channel.Subscriptions.TryAdd(id, subscription))
            {
                _logger.LogInformation("Subscribed [{sub}] on channel [{channel}]", subscription.Name, channelName);
            }
            else { } // can't happen due to atomic id increments above

            subscription.StartSubscription(token);

            return subscription;
        }
        else // not the type Subscribe caller was expecting
        {
            _logger.LogWarning("Failed to subscribe on channel [{channel}] with type [{subType}] as it's already registered with type [{regType}]", channelName, subType.Name, channel.BodyType.Name);
            throw new InvalidOperationException($"Can't subscribe on channel [{channelName}] with type [{subType.Name}] as it's already registered with type [{channel.BodyType.Name}]");
        }
    }

    private ISubscription SubscribeWildcard<TBody>(string pattern, Func<Message<TBody>, ValueTask> handler,
                                                      string? subscriptionName,
                                                      bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity,
                                                      TimeSpan conflationInterval,
                                                      CancellationToken token = default)
    {
        //todo: review adding wildcardSubscription to the registry here and adding one in the CreateNewChannel when publishing/subscribing potential RACE condition

        var wildcardSubscriptions = _wildcardSubscriptions.GetOrAdd(pattern, new ConcurrentDictionary<long, ISubscription>());

        //create, register and return subscription
        long id = Interlocked.Increment(ref _globalSubId);

        var stateFactories = new List<Func<IEnumerable<Message<TBody>>>>();

        var subscription = new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                   id, subscriptionName, pattern, bufferCapacity, conflationInterval, slowConsumerStrategy, handler,
                                                   () => Unsubscribe(pattern, id),
                                                   stateFactories, this, false, true);

        //re: race condition described above - it could be that someone published|subscribed and created a new channel matching this very same pattern by now
        //but our new subscription isn't in the registry yet, so that update is missed.
        //we will however FindMatchingChannels which will contain this channel and subscribe on it. If it has state, we'll fetch it too.

        if (!wildcardSubscriptions.TryAdd(id, subscription))
        {
            // can't happen as we equate subscriptions by their names which contain globalSubId which is unique
        }

        var channels = FindMatchingChannels(pattern);

        if (channels.Any())
        {
            var subType = typeof(TBody);

            foreach (var channel in channels)
            {
                // if channel was already there and its type matches the type passed with this call
                if (channel.BodyType == subType)
                {
                    if (fetchState)
                    {
                        var messageStore = channel.GetMessageStore<TBody>();
                        if (messageStore != null)
                        {
                            stateFactories.Add(messageStore.GetState);
                        }
                    }

                    if (channel.Subscriptions.TryAdd(id, subscription))
                    {
                        _logger.LogInformation("Subscribed [{sub}] on channel [{channel}]", subscription.Name, pattern);
                    }
                    else { } // can't happen due to atomic global subscription id increments
                }
                else // not the type Subscribe caller was expecting
                {
                    _logger.LogWarning("Can't subscribe on channel [{channel}] with type [{subType}] using wildcard [{wildcard}] as it's registered with a different type - [{regType}]",
                        channel.Name, subType.Name, pattern, channel.BodyType.Name);
                }
            }
        }

        subscription.StartSubscription(token);

        return subscription;
    }

    private IReadOnlyCollection<Channel> FindMatchingChannels(string pattern)
        => _channels.Where(kvp => MatchesChannelPattern(kvp.Key, pattern))
                    .Select(kvp => kvp.Value.Value)
                    .ToArray();

    private void ProcessWildcardSubscriptions(Channel channel)
    {
        foreach (var (pattern, subscriptions) in _wildcardSubscriptions)
        {
            if (MatchesChannelPattern(channel.Name, pattern))
            {
                foreach (var (id, subscription) in subscriptions)
                {
                    if (channel.BodyType == subscription.MessageBodyType)
                    {
                        if (channel.Subscriptions.TryAdd(id, subscription))
                        {
                            _logger.LogInformation("Subscribed [{sub}] on channel [{channel}]", subscription.Name, pattern);
                        }
                        else { } // can't happen due to atomic glocal subscription id increments
                    }
                }
            }
        }
    }

    private static bool MatchesChannelPattern(string channelName, string pattern)
    {
        int recursivePosition = pattern.IndexOf('>');

        if (recursivePosition > 0)
        {
            var prefix = pattern.AsSpan()[..recursivePosition];
            return channelName.AsSpan().StartsWith(prefix);
        }

        //todo: this is used only when subscribing/unsubscribing (rare) and creating a new channel (rare), so allocations don't matter in this case
        //it is however nice to change this to a span.slice by '.' to reduce allocations
        //also two overloads could be added, that receives channelParts and another one receiving patternParts for ProcessWildcardSubscriptions and FindMatchingChannels accordingly

        var channelParts = channelName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var patternParts = pattern.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (channelParts.Length != patternParts.Length)
            return false;

        int k = 0;
        while (k < channelParts.Length && (channelParts[k] == patternParts[k] || patternParts[k] == "*"))
        {
            k++;
        }

        return k == channelParts.Length;
    }

    private static bool IsWildcardSubscription(string channelName) => channelName.Contains('>') || channelName.Contains('*');

    private static bool IsSystemChannel(string channelName) => channelName.StartsWith("$");

    private ISubscription SubscribeSystem<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, string? subscriptionName, CancellationToken token = default)
    {
        var subType = typeof(TBody);

        if (IsWildcardSubscription(channelName))
        {
            throw new InvalidSubscriptionException("Can't wildcard subscribe on system channels");
        }

        var channel = GetSystemChannel(channelName);
        if (channel != null)
        {
            if (channel.BodyType == subType)
            {
                //create, register and return subscription
                long id = Interlocked.Increment(ref _globalSubId);

                var subscription = new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                               id, subscriptionName, channelName, 1000, TimeSpan.Zero, SlowConsumerStrategy.SkipUpdates, handler,
                                                               () => Unsubscribe(channelName, id),
                                                               null, this, true, false);

                if (channel.Subscriptions.TryAdd(id, subscription))
                {
                    _logger.LogInformation("Subscribed [{sub}] on channel [{channel}]", subscription.Name, channelName);
                }
                else { } // can't happen due to atomic id increments above

                subscription.StartSubscription(token);

                return subscription;
            }
            else // not the type Subscribe caller was expecting
            {
                _logger.LogWarning("Failed to subscribe on channel [{channel}] with type [{subType}] as it's already registered with type [{regType}]", channelName, subType.Name, channel.BodyType.Name);
                throw new InvalidOperationException($"Can't subscribe on channel [{channelName}] with type [{subType.Name}] as it's already registered with type [{channel.BodyType.Name}]");
            }
        }
        else
        {
            throw new InvalidSubscriptionException($"No system channel [{channelName}] found. Is MessageTracing enabled?");
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
                //todo: broadcast deletion
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
                //todo: broadcast reset
            }
        }

        return false;
    }

    public IReadOnlyList<ChannelInfo> GetChannels()
    {
        EnsureNotDisposed();

        return _channels
                    .Where(kvp => !kvp.Key.StartsWith("$"))
                    .Select(kvp => new ChannelInfo
                    {
                        Name = kvp.Key,
                        BodyType = kvp.Value.Value.BodyType,
                        Statistics = kvp.Value.Value.Statistics,
                        LastPublishedAt = kvp.Value.Value.LastPublishedAt,
                        LastPublishedBy = kvp.Value.Value.LastPublishedBy
                    }).ToList();
    }

    public IReadOnlyCollection<SubscriptionInfo> GetChannelSubscriptions(string channelName)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            return channel.Value.Subscriptions
                .Select(kvp => new SubscriptionInfo
                {
                    Name = kvp.Value.Name,
                    SubscribedOn = kvp.Value.SubscribedOn,
                    ConflationInterval = kvp.Value.ConflationInterval,
                    Statistics = kvp.Value.Statistics
                })
                .ToList();
        }

        return Array.Empty<SubscriptionInfo>();
    }

    public long GetNextCorrelationId() => Interlocked.Increment(ref _globalCorrelationId);

    public bool TryDeleteChannel(string channelName)
    {
        if (!_channels.TryRemove(channelName, out var channelProxy)) return false;

        foreach (var (_, subscription) in channelProxy.Value.Subscriptions)
        {
            if (!subscription.IsWildcard)
            {
                subscription.TryDispose();
            }
        }

        return true;

    }

    private Channel? GetSystemChannel(string channel)
    {
        if (channel.StartsWith("$") && _channels.TryGetValue(channel, out var channelProxy))
        {
            return channelProxy.Value;
        }

        return null;
    }

    private Channel GetOrAddChannel(string channelName, Type bodyType)
    {
        //ConcurrentDictionary's create factory can be called multiple times but only one result wins and gets added as a value for that key
        //By wrapping it with Lazy, we ensure we materialise it only once (.Value)
        return _channels.GetOrAdd(channelName,
            c => new Lazy<Channel>(() =>
            {
                _logger.LogInformation("Channel [{channel}] for type [{bodyType}] is created.", c, bodyType);

                var channel = new Channel
                {
                    BodyType = bodyType,
                    Name = c
                };

                ProcessWildcardSubscriptions(channel);

                return channel;
            }))
            .Value;
    }

    private void Unsubscribe(string channelName, long id)
    {
        if (IsWildcardSubscription(channelName))
        {
            var channels = FindMatchingChannels(channelName);
            foreach (var channel in channels)
            {
                Remove(channel);
            }

            _ = _wildcardSubscriptions.TryGetValue(channelName, out var subscriptions) && subscriptions.TryRemove(id, out _);
        }
        else if (_channels.TryGetValue(channelName, out var channel))
        {
            Remove(channel.Value);
        }

        void Remove(Channel channel)
        {
            if (channel.Subscriptions.TryRemove(id, out var sub))
            {
                _logger.LogInformation("Unsubscribed [{sub}] from channel [{channel}]", sub.Name, channelName);
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

    [LoggerMessage(0, LogLevel.Trace, "Sent message [{messageId}] | correlation [{corId}] | key [{key}] to subscription [{subscriptionName}] on channel [{channel}]")]
    partial void LogMessageSent(long messageId, long corId, string key, string subscriptionName, string channel);
}