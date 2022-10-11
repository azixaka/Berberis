namespace Berberis.Messaging;

partial class CrossBar
{
    public ValueTask Publish<TBody>(string channelName, TBody body) => Publish(channelName, body, 0, null, false, null);
    public ValueTask Publish<TBody>(string channelName, TBody body, string from) => Publish(channelName, body, 0, null, false, from);
    public ValueTask Publish<TBody>(string channelName, TBody body, long correlationId) => Publish(channelName, body, correlationId, null, false, null);
    public ValueTask Publish<TBody>(string channelName, TBody body, string key, bool store) => Publish(channelName, body, 0, key, store, null);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, CancellationToken token)
        => Subscribe(channelName, handler, null, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, token);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, string subscriptionName, CancellationToken token)
        => Subscribe(channelName, handler, subscriptionName, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, token);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, bool fetchState, CancellationToken token)
        => Subscribe(channelName, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, token);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, CancellationToken token)
        => Subscribe(channelName, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, token);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, bool fetchState, TimeSpan conflationInterval, CancellationToken token)
        => Subscribe(channelName, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval, token);

    public ISubscription Subscribe<TBody>(string channelName, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, TimeSpan conflationInterval, CancellationToken token)
        => Subscribe(channelName, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval, token);
}