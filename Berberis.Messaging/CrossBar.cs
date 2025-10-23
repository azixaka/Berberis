using Berberis.Messaging.Exceptions;
using Berberis.Messaging.Statistics;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Berberis.Messaging;

/// <summary>High-performance typed pub/sub message broker.</summary>
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

    /// <summary>System channel for message traces.</summary>
    public string TracingChannel { get; } = "$message.traces";

    /// <summary>Enable message tracing to TracingChannel.</summary>
    public bool MessageTracingEnabled
    {
        get => _tracingEnabled;
        set
        {
            if (value && _tracingEnabled != value)
            {
                _ = GetOrAddChannel<MessageTrace>(TracingChannel, typeof(MessageTrace));
                _tracingEnabled = value;
            }
        }
    }

    /// <summary>Enable verbose publish logging.</summary>
    public bool PublishLoggingEnabled { get; set; }

    /// <summary>Creates a new CrossBar instance.</summary>
    /// <param name="loggerFactory">Logger factory.</param>
    public CrossBar(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CrossBar>();
    }

    /// <summary>Publishes a message to a channel.</summary>
    /// <param name="channelName">Channel name.</param>
    /// <param name="message">Message to publish.</param>
    /// <param name="store">Store in channel state.</param>
    /// <exception cref="FailedPublishException">Store=true but key is missing.</exception>
    /// <exception cref="ChannelTypeMismatchException">Type mismatch.</exception>
    public ValueTask Publish<TBody>(string channelName, Message<TBody> message, bool store)
    {
        var ticks = StatsTracker.GetTicks();

        EnsureNotDisposed();
        ValidateChannelName(channelName, nameof(channelName));

        if (store && string.IsNullOrEmpty(message.Key))
        {
            throw new FailedPublishException($"Stored message must have key specified. Channel: {channelName}");
        }

        var pubType = typeof(TBody);

        var channel = GetOrAddChannel<TBody>(channelName, pubType);

        // if channel was already there and its type matches the type passed with this call
        if (pubType == channel.BodyType)
        {
            message.InceptionTicks = ticks;

            if (message.Id < 0)
            {
                message.Id = channel.NextMessageId();
            }

            if (MessageTracingEnabled)
            {
                PublishSystem(TracingChannel,
                                new MessageTrace
                                {
                                    MessageKey = message.Key,
                                    CorrelationId = message.CorrelationId,
                                    From = message.From,
                                    Channel = channelName,
                                    Ticks = ticks
                                });
            }

            if (store)
            {
                var messageStore = channel.GetOrCreateMessageStore<TBody>();
                messageStore.Update(message);
            }

            channel.LastPublishedAt = DateTime.FromBinary(message.Timestamp);
            channel.LastPublishedBy = message.From;

            var enumerator = channel.SubscriptionsEnumerator.Value;
            enumerator!.Reset();

            // walk through all the subscriptions on this channel...
            while (enumerator.MoveNext())
            {
                var (_, subObj) = enumerator.Current;

                //foreach (var (_, subObj) in channel.Subscriptions)
                //{
                if (!subObj.IsDetached && subObj is Subscription<TBody> subscription) // Subscribe method ensures this will always cast successfully
                {
                    // and send the body wrapped into a Message envelope with some metadata
                    if (subscription.TryWrite(message))
                    {
                        if (PublishLoggingEnabled)
                        {
                            LogMessageSent(message.Id, message.CorrelationId, message.Key ?? string.Empty, subscription.Name, channelName);
                        }
                    }
                    else
                    {
                        switch (subscription.SlowConsumerStrategy)
                        {
                            case SlowConsumerStrategy.SkipUpdates:
                                _logger.LogWarning("Subscription [{sub}] SKIPPED message [{msgId}] | correlation [{corId}] on channel [{channel}]",
                                    subscription.Name, message.Id, message.CorrelationId, channelName);
                                break;

                            case SlowConsumerStrategy.FailSubscription:
                                _ = subscription.TryFail(_failedSubscriptionException);

                                _logger.LogWarning("Subscription [{sub}] FAILED to receive message [{msgId}] | correlation [{corId}] on channel [{channel}]",
                                   subscription.Name, message.Id, message.CorrelationId, channelName);
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

            return ValueTask.FromException(new ChannelTypeMismatchException(channelName, channel.BodyType, pubType));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes a message body to a channel.</summary>
    /// <param name="channelName">Channel name.</param>
    /// <param name="body">Message body.</param>
    /// <param name="correlationId">Correlation ID.</param>
    /// <param name="key">State key (required if store=true).</param>
    /// <param name="store">Store in channel state.</param>
    /// <param name="from">Source identifier.</param>
    /// <param name="tagA">Custom metadata tag.</param>
    public ValueTask Publish<TBody>(string channelName, TBody body, long correlationId, string? key, bool store, string? from, long tagA)
    {
        var message = new Message<TBody>(-1, DateTime.UtcNow.ToBinary(), MessageType.ChannelUpdate, correlationId, key, 0, from, body, tagA);

        return Publish(channelName, message, store);
    }

    internal ValueTask PublishSystem<TBody>(string channelName, TBody body)
    {
        var channel = GetSystemChannel(channelName);
        if (channel != null)
        {
            var msg = new Message<TBody>(0, 0, MessageType.SystemTrace, 0, null, 0, null, body, 0);

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

    /// <summary>Subscribes to channel messages.</summary>
    /// <param name="channelName">Channel or wildcard pattern.</param>
    /// <param name="handler">Message handler.</param>
    /// <param name="options">Subscription options.</param>
    /// <param name="token">Cancellation token.</param>
    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          SubscriptionOptions? options,
                                          CancellationToken token = default)
    {
        return Subscribe(channelName, handler, null, false, SlowConsumerStrategy.SkipUpdates, null,
                        TimeSpan.Zero, default, options, token);
    }

    /// <summary>Subscribes to channel messages with name.</summary>
    /// <param name="channelName">Channel or wildcard pattern.</param>
    /// <param name="handler">Message handler.</param>
    /// <param name="subscriptionName">Subscription name.</param>
    /// <param name="options">Subscription options.</param>
    /// <param name="token">Cancellation token.</param>
    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          string? subscriptionName,
                                          SubscriptionOptions? options,
                                          CancellationToken token = default)
    {
        return Subscribe(channelName, handler, subscriptionName, false, SlowConsumerStrategy.SkipUpdates, null,
                        TimeSpan.Zero, default, options, token);
    }

    /// <summary>Subscribes to channel messages with advanced options.</summary>
    /// <param name="channelName">Channel or wildcard pattern.</param>
    /// <param name="handler">Message handler.</param>
    /// <param name="subscriptionName">Subscription name.</param>
    /// <param name="fetchState">Deliver stored state first.</param>
    /// <param name="slowConsumerStrategy">Backpressure handling strategy.</param>
    /// <param name="bufferCapacity">Buffer size.</param>
    /// <param name="conflationInterval">Message conflation interval.</param>
    /// <param name="subscriptionStatsOptions">Statistics options.</param>
    /// <param name="token">Cancellation token.</param>
    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          string? subscriptionName,
                                          bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity,
                                          TimeSpan conflationInterval,
                                          StatsOptions subscriptionStatsOptions,
                                          CancellationToken token = default)
    {
        return Subscribe(channelName, handler, subscriptionName, fetchState, slowConsumerStrategy, bufferCapacity, conflationInterval, subscriptionStatsOptions, null, token);
    }

    /// <summary>Subscribes to channel messages with full options.</summary>
    /// <param name="channelName">Channel or wildcard pattern.</param>
    /// <param name="handler">Message handler.</param>
    /// <param name="subscriptionName">Subscription name.</param>
    /// <param name="fetchState">Deliver stored state first.</param>
    /// <param name="slowConsumerStrategy">Backpressure handling strategy.</param>
    /// <param name="bufferCapacity">Buffer size.</param>
    /// <param name="conflationInterval">Message conflation interval.</param>
    /// <param name="subscriptionStatsOptions">Statistics options.</param>
    /// <param name="options">Subscription options (timeout, etc).</param>
    /// <param name="token">Cancellation token.</param>
    /// <exception cref="ChannelTypeMismatchException">Type mismatch.</exception>
    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler,
                                          string? subscriptionName,
                                          bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity,
                                          TimeSpan conflationInterval,
                                          StatsOptions subscriptionStatsOptions,
                                          SubscriptionOptions? options,
                                          CancellationToken token = default)
    {
        EnsureNotDisposed();
        ValidateChannelName(channelName, nameof(channelName));
        ValidateHandler(handler);

        if (IsSystemChannel(channelName))
        {
            return SubscribeSystem(channelName, handler, subscriptionName, token);
        }

        if (IsWildcardSubscription(channelName))
        {
            return SubscribeWildcard(channelName, handler, subscriptionName, fetchState, slowConsumerStrategy, bufferCapacity, conflationInterval, subscriptionStatsOptions, options, token);
        }

        var subType = typeof(TBody);

        var channel = GetOrAddChannel<TBody>(channelName, subType);

        // if channel was already there and its type matches the type passed with this call
        if (channel.BodyType == subType)
        {
            Func<IEnumerable<Message<TBody>>>? stateFactory = null;

            if (fetchState)
            {
                var messageStore = channel.GetOrCreateMessageStore<TBody>();
                stateFactory = messageStore.GetState;
            }

            //create, register and return subscription
            long id = Interlocked.Increment(ref _globalSubId);

            var subscription = new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                       id, subscriptionName, channelName, bufferCapacity, conflationInterval, slowConsumerStrategy, handler,
                                                       () => Unsubscribe(channelName, id),
                                                       stateFactory != null ? new[] { stateFactory } : null, this, false, false, subscriptionStatsOptions, options);

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
            throw new ChannelTypeMismatchException(channelName, channel.BodyType, subType);
        }
    }

    private ISubscription SubscribeWildcard<TBody>(string pattern, Func<Message<TBody>, ValueTask> handler,
                                                      string? subscriptionName,
                                                      bool fetchState, SlowConsumerStrategy slowConsumerStrategy, int? bufferCapacity,
                                                      TimeSpan conflationInterval,
                                                      StatsOptions subscriptionStatsOptions,
                                                      SubscriptionOptions? options = null,
                                                      CancellationToken token = default)
    {
        // KNOWN LIMITATION: Potential race condition in wildcard subscription registration
        //
        // Scenario:
        //   1. Thread A: Subscribes with pattern "orders.*"
        //   2. Thread B: Publishes to "orders.new" (creates channel)
        //   3. Thread A: Registers wildcard subscription
        //   4. Result: Message from step 2 might be missed
        //
        // MITIGATION (already implemented):
        //   After registering wildcard subscription, FindMatchingChannels() is called (line 265)
        //   which subscribes to all existing channels matching the pattern. This ensures
        //   subsequent messages are received, though the initial message may be lost.
        //
        // DESIGN DECISION:
        //   Eventual consistency model - Wildcard subscriptions catch up to existing
        //   channels within microseconds. The alternative (global lock) would severely
        //   impact performance. This is acceptable for the target use cases (high-frequency
        //   trading, real-time analytics) where:
        //   - Subscriptions are typically created before heavy message traffic
        //   - Missing one initial message is acceptable (stateful channels provide catchup)
        //   - Performance is critical
        //
        // If guaranteed delivery is required:
        //   1. Create wildcard subscription BEFORE any publishing begins, OR
        //   2. Use stateful channels with fetchState: true to catch up on missed messages, OR
        //   3. Subscribe to specific channels (no wildcards) for critical paths

        var wildcardSubscriptions = _wildcardSubscriptions.GetOrAdd(pattern, new ConcurrentDictionary<long, ISubscription>());

        //create, register and return subscription
        long id = Interlocked.Increment(ref _globalSubId);

        var stateFactories = new List<Func<IEnumerable<Message<TBody>>>>();

        var subscription = new Subscription<TBody>(_loggerFactory.CreateLogger<Subscription<TBody>>(),
                                                   id, subscriptionName, pattern, bufferCapacity, conflationInterval, slowConsumerStrategy, handler,
                                                   () => Unsubscribe(pattern, id),
                                                   stateFactories, this, false, true, subscriptionStatsOptions, options);

        //re: race condition described above - it could be that someone published|subscribed and created a new channel matching this very same pattern by now
        //but our new subscription isn't in the registry yet, so that update is missed.
        //we will howerver FindMatchingChannels which will contain this channel and subscribe on it. If it has state, we'll fetch it too.

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
                        var messageStore = channel.GetOrCreateMessageStore<TBody>();
                        stateFactories.Add(messageStore.GetState);
                    }

                    if (channel.Subscriptions.TryAdd(id, subscription))
                    {
                        _logger.LogInformation("Subscribed [{sub}] on channel [{channel}]", subscription.Name, pattern);
                    }
                    else { } // can't happen due to atomic glocal subscription id increments
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
    {
        var result = new List<Channel>();
        foreach (var kvp in _channels)
        {
            if (MatchesChannelPattern(kvp.Key, pattern))
            {
                result.Add(kvp.Value.Value);
            }
        }
        return result.ToArray();
    }

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

    private bool MatchesChannelPattern(string channelName, string pattern)
    {
        int recursivePosition = pattern.IndexOf('>');

        if (recursivePosition > 0)
        {
            var prefix = pattern.AsSpan().Slice(0, recursivePosition);
            return channelName.AsSpan().StartsWith(prefix);
        }

        // Span-based segment matching (allocation-free)
        ReadOnlySpan<char> channelSpan = channelName.AsSpan();
        ReadOnlySpan<char> patternSpan = pattern.AsSpan();

        int channelSegments = CountSegments(channelSpan);
        int patternSegments = CountSegments(patternSpan);

        if (channelSegments != patternSegments)
            return false;

        int channelPos = 0;
        int patternPos = 0;

        for (int i = 0; i < channelSegments; i++)
        {
            var channelSegment = GetNextSegment(channelSpan, ref channelPos);
            var patternSegment = GetNextSegment(patternSpan, ref patternPos);

            // Wildcard '*' matches any segment
            if (patternSegment.Length == 1 && patternSegment[0] == '*')
                continue;

            // Segments must match exactly
            if (!channelSegment.SequenceEqual(patternSegment))
                return false;
        }

        return true;
    }

    private static int CountSegments(ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
            return 0;

        int count = 0;
        bool inSegment = false;

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == '.')
            {
                inSegment = false;
            }
            else if (!inSegment)
            {
                inSegment = true;
                count++;
            }
        }

        return count;
    }

    private static ReadOnlySpan<char> GetNextSegment(ReadOnlySpan<char> span, ref int pos)
    {
        // Skip dots (handles empty segments like RemoveEmptyEntries)
        while (pos < span.Length && span[pos] == '.')
            pos++;

        if (pos >= span.Length)
            return ReadOnlySpan<char>.Empty;

        int start = pos;

        // Find end of segment
        while (pos < span.Length && span[pos] != '.')
            pos++;

        return span.Slice(start, pos - start);
    }

    private static bool IsWildcardSubscription(string channelName) => channelName.Contains(">") || channelName.Contains("*");

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
                                                               null, this, true, false, default);

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
                throw new ChannelTypeMismatchException(channelName, channel.BodyType, subType);
            }
        }
        else
        {
            throw new InvalidSubscriptionException($"No system channel [{channelName}] found. Is MessageTracing enabled?");
        }
    }

    /// <summary>Gets all stored messages for a channel.</summary>
    /// <param name="channelName">Channel name.</param>
    public IEnumerable<Message<TBody>> GetChannelState<TBody>(string channelName)
    {
        var messageStore = GetChannelStore<TBody>(channelName);
        if (messageStore != null)
        {
            return messageStore.GetState();
        }

        return Array.Empty<Message<TBody>>();
    }

    /// <summary>Gets a stored message by key.</summary>
    /// <param name="channelName">Channel name.</param>
    /// <param name="key">Message key.</param>
    /// <param name="message">Retrieved message.</param>
    public bool TryGetMessage<TBody>(string channelName, string key, out Message<TBody> message)
    {
        var messageStore = GetChannelStore<TBody>(channelName);
        if (messageStore != null)
        {
            return messageStore.TryGet(key, out message);
        }

        message = default;
        return false;
    }

    /// <summary>Deletes a stored message by key.</summary>
    /// <param name="channelName">Channel name.</param>
    /// <param name="key">Message key.</param>
    public bool TryDeleteMessage<TBody>(string channelName, string key)
    {
        var messageStore = GetChannelStore<TBody>(channelName);

        if (messageStore != null)
        {
            var deleted = messageStore.TryDelete(key);
            if (deleted)
            {
                Publish(channelName, new Message<TBody>(0, 0, MessageType.ChannelDelete, 0, null, 0, null, default, 0));
                return true;
            }
        }

        return false;
    }

    /// <summary>Clears all stored messages in a channel.</summary>
    /// <param name="channelName">Channel name.</param>
    public void ResetChannel<TBody>(string channelName)
    {
        var messageStore = GetChannelStore<TBody>(channelName);

        if (messageStore != null)
        {
            messageStore.Reset();
        }

        Publish(channelName, new Message<TBody>(0, 0, MessageType.ChannelReset, 0, null, 0, null, default, 0));
    }

    /// <summary>Deletes a channel and disposes subscriptions.</summary>
    /// <param name="channelName">Channel name.</param>
    public bool TryDeleteChannel(string channelName)
    {
        if (_channels.TryRemove(channelName, out var channelProxy))
        {
            foreach (var (_, subscription) in channelProxy.Value.Subscriptions)
            {
                if (!subscription.IsWildcard)
                {
                    // Notify subscription before disposal (best-effort delivery)
                    subscription.NotifyChannelDeletion();
                    subscription.TryDispose();
                }
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MessageStore<TBody>? GetChannelStore<TBody>(string channelName)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            return channel.Value.GetMessageStore<TBody>();
        }

        return null;
    }

    /// <summary>Gets all channels.</summary>
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

    /// <summary>Gets subscriptions for a channel.</summary>
    /// <param name="channelName">Channel name.</param>
    public IReadOnlyCollection<SubscriptionInfo>? GetChannelSubscriptions(string channelName)
    {
        if (_channels.TryGetValue(channelName, out var channel))
        {
            return channel.Value.Subscriptions
                .Select(kvp =>
                {
                    return new SubscriptionInfo
                    {
                        Name = kvp.Value.Name,
                        ChannelName = kvp.Value.ChannelName,
                        IsWildcard= kvp.Value.IsWildcard,
                        SubscribedOn = kvp.Value.SubscribedOn,
                        ConflationInterval = kvp.Value.ConflationInterval,
                        Statistics = kvp.Value.Statistics
                    };
                })
                .ToList();
        }

        return null;
    }

    /// <summary>Generates next correlation ID.</summary>
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
        // Publish on a channel happens very often while channel is created only once
        // this line first prevent the Func<string, Lazy> from being created and allocated on every single access to the same channel
        if (_channels.TryGetValue(channel, out var channelProxy))
        {
            return channelProxy.Value;
        }

        return CreateChannel<TBody>(channel, bodyType);
    }

    private Channel CreateChannel<TBody>(string channel, Type bodyType)
    {
        //ConcurrentDictionary's create factory can be called multiple times but only one result wins and gets added as a value for that key
        //By wrapping it with Lazy, we ensure we materialise it only once (.Value)
        return _channels.GetOrAdd(channel,
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

    /// <summary>Disposes CrossBar and all subscriptions.</summary>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateChannelName(string channelName, string paramName)
    {
        if (channelName == null)
            throw new ArgumentNullException(paramName, "Channel name cannot be null");

        if (string.IsNullOrWhiteSpace(channelName))
            throw new InvalidChannelNameException(channelName, "cannot be empty or whitespace");

        if (channelName.Length > 256)
            throw new InvalidChannelNameException(channelName, "too long (max 256 characters)");

        if (channelName.Contains(".."))
            throw new InvalidChannelNameException(channelName, "cannot contain consecutive dots");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateHandler<TBody>(Func<Message<TBody>, ValueTask> handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler), "Message handler cannot be null");
    }

    [LoggerMessage(0, LogLevel.Trace, "Sent message [{messageId}] | correlation [{corId}] | key [{key}] to subscription [{subscriptionName}] on channel [{channel}]")]
    partial void LogMessageSent(long messageId, long corId, string key, string subscriptionName, string channel);   
}