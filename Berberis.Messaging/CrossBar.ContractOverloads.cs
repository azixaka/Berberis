namespace Berberis.Messaging
{
    partial class CrossBar
    {
        public ValueTask Publish<TBody>(string channel, TBody body) => Publish(channel, body, 0, null, false, null);
        public ValueTask Publish<TBody>(string channel, TBody body, string from) => Publish(channel, body, 0, null, false, from);
        public ValueTask Publish<TBody>(string channel, TBody body, long correlationId) => Publish(channel, body, correlationId, null, false, null);
        public ValueTask Publish<TBody>(string channel, TBody body, string key, bool store) => Publish(channel, body, 0, key, store, null);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler)
       => Subscribe(channel, handler, null, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName)
            => Subscribe(channel, handler, subscriptionName, false, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState)
            => Subscribe(channel, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState)
            => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, TimeSpan.Zero);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, TimeSpan conflationInterval)
            => Subscribe(channel, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, TimeSpan conflationInterval)
            => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationInterval);
    }
}
