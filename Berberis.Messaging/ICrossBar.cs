namespace Berberis.Messaging;

public interface ICrossBar
{
    IReadOnlyList<CrossBar.ChannelInfo> GetChannels();

    IReadOnlyCollection<CrossBar.SubscriptionInfo> GetChannelSubscriptions(string channelName);

    ValueTask Publish<TBody>(string channel, TBody body);
    ValueTask Publish<TBody>(string channel, TBody body, string from);
    ValueTask Publish<TBody>(string channel, TBody body, string key, bool store);
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId);
    ValueTask Publish<TBody>(string channel, TBody body, long correlationId, string key, bool store, string from);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, TimeSpan conflationInterval, CancellationToken token = default);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, TimeSpan conflationInterval, CancellationToken token = default);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       string subscriptionName,
       bool fetchState,
       SlowConsumerStrategy slowConsumerStrategy,
       int? bufferCapacity,
       TimeSpan conflationInterval,
       CancellationToken token = default);

    bool TryDeleteMessage<TBody>(string channelName, string key, out Message<TBody> message);
    bool ResetStore<TBody>(string channelName);

    long GetNextCorrelationId();

    bool TracingEnabled { get; set; }
}