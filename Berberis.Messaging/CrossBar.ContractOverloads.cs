using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

partial class CrossBar
{
    public ValueTask Publish<TBody>(string channel, Message<TBody> message) => Publish(channel, message, false);

    public ValueTask Publish<TBody>(string channel, TBody body) => Publish(channel, body, 0, null, false, null, 0);
    public ValueTask Publish<TBody>(string channel, TBody body, string from) => Publish(channel, body, 0, null, false, from, 0);
    public ValueTask Publish<TBody>(string channel, TBody body, long correlationId) => Publish(channel, body, correlationId, null, false, null, 0);    
    public ValueTask Publish<TBody>(string channel, TBody body, string key, bool store) => Publish(channel, body, 0, key, store, null, 0);
    public ValueTask Publish<TBody>(string channel, TBody body, long correlationId, string key, bool store, string from) 
                                                        => Publish(channel, body, correlationId, key, store, from, 0);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, CancellationToken token)
        => Subscribe(channel, handler, null, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, default, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, CancellationToken token)
        => Subscribe(channel, handler, subscriptionName, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, default, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, CancellationToken token)
        => Subscribe(channel, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, default, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, CancellationToken token)
        => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, default, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, TimeSpan conflationInterval, CancellationToken token)
        => Subscribe(channel, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval, default, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, TimeSpan conflationInterval, CancellationToken token)
        => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval, default, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, StatsOptions statsOptions, CancellationToken token)
       => Subscribe(channel, handler, subscriptionName, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero, statsOptions, token);

    public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, TimeSpan conflationInterval, StatsOptions statsOptions, CancellationToken token)
        => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval, statsOptions, token);
}