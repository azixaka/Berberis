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

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState);
    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, int conflationIntervalMilliseconds);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
       bool fetchState,
       SlowConsumerStrategy slowConsumerStrategy,
       int? bufferCapacity,
       int conflationIntervalMilliseconds);

    bool TryDeleteMessage<TBody>(string channelName, string key, out Message<TBody> message);
    bool ResetStore<TBody>(string channelName);

    long GetNextCorrelationId();

    bool TracingEnabled { get; set; }
}