namespace Berberis.Messaging;

public interface ICrossBar
{
    IReadOnlyList<CrossBar.ChannelInfo> GetChannels();

    IReadOnlyCollection<CrossBar.SubscriptionInfo>? GetChannelSubscriptions(string channelName);

    ValueTask Publish<TBody>(string channel, Message<TBody> message);
    ValueTask Publish<TBody>(string channel, Message<TBody> message, bool store);

    ValueTask Publish<TBody>(string channel, TBody body);
    ValueTask Publish<TBody>(string channel, TBody body, string from);
    ValueTask Publish<TBody>(string channel, TBody body, string key, bool store);
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId);
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId, string key, bool store, string from);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool includeP90Stats = false, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool includeP90Stats = false, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, bool includeP90Stats = false, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, bool includeP90Stats = false, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, TimeSpan conflationInterval, bool includeP90Stats = false, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, TimeSpan conflationInterval, bool includeP90Stats = false, CancellationToken token = default);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       string subscriptionName,
       bool fetchState,
       SlowConsumerStrategy slowConsumerStrategy,
       int? bufferCapacity,
       TimeSpan conflationInterval,
       bool includeP90Stats = false,
       CancellationToken token = default);

    IEnumerable<Message<TBody>> GetChannelState<TBody>(string channelName);
    bool TryGetMessage<TBody>(string channelName, string key, out Message<TBody> message);
    bool TryDeleteMessage<TBody>(string channelName, string key);
    void ResetChannel<TBody>(string channelName);
    bool TryDeleteChannel(string channelName);

    long GetNextCorrelationId();

    bool MessageTracingEnabled { get; set; }

    bool PublishLoggingEnabled { get; set; }
}