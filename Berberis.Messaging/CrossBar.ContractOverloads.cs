namespace Berberis.Messaging
{
    partial class CrossBar
    {
        public ValueTask Publish<TBody>(string channel, TBody body) => Publish(channel, body, 0, null, false, null);
        public ValueTask Publish<TBody>(string channel, TBody body, string from) => Publish(channel, body, 0, null, false, from);
        public ValueTask Publish<TBody>(string channel, TBody body, long correlationId) => Publish(channel, body, correlationId, null, false, null);
        public ValueTask Publish<TBody>(string channel, TBody body, string key, bool store) => Publish(channel, body, 0, key, store, null);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler)
       => Subscribe(channel, handler, null, false, SlowConsumerStrategy.SkipUpdates, null, -1);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName)
            => Subscribe(channel, handler, subscriptionName, false, SlowConsumerStrategy.SkipUpdates, null, -1);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState)
            => Subscribe(channel, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, -1);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState)
            => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, -1);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, bool fetchState, int conflationIntervalMilliseconds)
            => Subscribe(channel, handler, null, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationIntervalMilliseconds);

        public ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, string subscriptionName, bool fetchState, int conflationIntervalMilliseconds)
            => Subscribe(channel, handler, subscriptionName, fetchState, SlowConsumerStrategy.SkipUpdates, null, conflationIntervalMilliseconds);
    }
}
