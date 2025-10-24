using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

/// <summary>Message broker interface.</summary>
public interface ICrossBar
{
    /// <summary>Gets all channels.</summary>
    IReadOnlyList<CrossBar.ChannelInfo> GetChannels();

    /// <summary>Gets subscriptions for a channel.</summary>
    IReadOnlyCollection<CrossBar.SubscriptionInfo>? GetChannelSubscriptions(string channelName);

    /// <summary>Publishes a message.</summary>
    ValueTask Publish<TBody>(string channel, Message<TBody> message);
    /// <summary>Publishes a message with store option.</summary>
    ValueTask Publish<TBody>(string channel, Message<TBody> message, bool store);

    /// <summary>Publishes a message body.</summary>
    ValueTask Publish<TBody>(string channel, TBody body);
    /// <summary>Publishes a message body with source.</summary>
    ValueTask Publish<TBody>(string channel, TBody body, string from);
    /// <summary>Publishes a message body with key.</summary>
    ValueTask Publish<TBody>(string channel, TBody body, string key, bool store);
    /// <summary>Publishes a message body with correlation.</summary>
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId);
    /// <summary>Publishes a message body with full options.</summary>
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId, string key, bool store, string from);
    /// <summary>Publishes a message body with all metadata.</summary>
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId, string key, bool store, string from, long tagA);

    /// <summary>Subscribes to channel.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, CancellationToken token = default);
    /// <summary>Subscribes to channel with name.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string? subscriptionName, CancellationToken token = default);
    /// <summary>Subscribes to channel with options.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, SubscriptionOptions? options, CancellationToken token = default);
    /// <summary>Subscribes to channel with name and options.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string? subscriptionName, SubscriptionOptions? options, CancellationToken token = default);
    /// <summary>Subscribes to channel with state.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, CancellationToken token = default);
    /// <summary>Subscribes to channel with name and state.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string? subscriptionName, bool fetchState, CancellationToken token = default);
    /// <summary>Subscribes to channel with state and conflation.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, TimeSpan conflationInterval, CancellationToken token = default);
    /// <summary>Subscribes to channel with full options.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string? subscriptionName, bool fetchState, TimeSpan conflationInterval, CancellationToken token = default);

    /// <summary>Subscribes to channel with stats.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       string? subscriptionName,
       StatsOptions statsOptions,
       CancellationToken token = default);

    /// <summary>Subscribes to channel with advanced options.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       string? subscriptionName,
       bool fetchState,
       TimeSpan conflationInterval,
       StatsOptions statsOptions,
       CancellationToken token = default);

    /// <summary>Subscribes to channel with full options.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       string? subscriptionName,
       bool fetchState,
       SlowConsumerStrategy slowConsumerStrategy,
       int? bufferCapacity,
       TimeSpan conflationInterval,
       StatsOptions statsOptions,
       CancellationToken token = default);

    /// <summary>Subscribes to channel with all options.</summary>
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       string? subscriptionName,
       bool fetchState,
       SlowConsumerStrategy slowConsumerStrategy,
       int? bufferCapacity,
       TimeSpan conflationInterval,
       StatsOptions statsOptions,
       SubscriptionOptions? options,
       CancellationToken token = default);

    /// <summary>Gets all stored messages.</summary>
    IEnumerable<Message<TBody>> GetChannelState<TBody>(string channelName);
    /// <summary>Gets stored message by key.</summary>
    bool TryGetMessage<TBody>(string channelName, string key, out Message<TBody> message);
    /// <summary>Deletes stored message by key.</summary>
    bool TryDeleteMessage<TBody>(string channelName, string key);
    /// <summary>Clears all stored messages.</summary>
    void ResetChannel<TBody>(string channelName);
    /// <summary>Deletes channel and subscriptions.</summary>
    bool TryDeleteChannel(string channelName);

    /// <summary>Generates next correlation ID.</summary>
    long GetNextCorrelationId();

    /// <summary>Enable message tracing.</summary>
    bool MessageTracingEnabled { get; set; }

    /// <summary>Enable lifecycle event tracking.</summary>
    bool LifecycleTrackingEnabled { get; set; }

    /// <summary>Enable verbose publish logging.</summary>
    bool PublishLoggingEnabled { get; set; }
}
