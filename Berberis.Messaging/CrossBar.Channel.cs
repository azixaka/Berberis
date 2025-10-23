using System.Collections.Concurrent;
using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal record Channel
    {
        private long _channelSequenceId;

        private readonly ConcurrentDictionary<Type, object> _lazyMessageStores = new();

        public long NextMessageId() => Interlocked.Increment(ref _channelSequenceId);

        public required Type BodyType { get; init; }

        public required string Name { get; init; }

        public Channel()
        {
            Subscriptions = new ConcurrentDictionary<long, ISubscription>();
            SubscriptionsEnumerator = new ThreadLocal<IEnumerator<KeyValuePair<long, ISubscription>>>(() => Subscriptions.GetEnumerator());
        }

        public ConcurrentDictionary<long, ISubscription> Subscriptions { get; }

        public ThreadLocal<IEnumerator<KeyValuePair<long, ISubscription>>> SubscriptionsEnumerator { get; }

        public ChannelStatsTracker Statistics { get; } = new ChannelStatsTracker();

        public DateTime LastPublishedAt { get; internal set; }

        public string? LastPublishedBy { get; internal set; }

        public MessageStore<TBody> GetOrCreateMessageStore<TBody>()
        {
            // Hot path - check if already initialized (no allocation)
            if (_lazyMessageStores.TryGetValue(typeof(TBody), out var lazy))
            {
                return ((Lazy<MessageStore<TBody>>)lazy).Value;
            }

            // Cold path - first time initialization (one-time allocation per type)
            var newLazy = new Lazy<MessageStore<TBody>>(() => new MessageStore<TBody>());
            lazy = _lazyMessageStores.GetOrAdd(typeof(TBody), newLazy);
            return ((Lazy<MessageStore<TBody>>)lazy).Value;
        }

        public MessageStore<TBody>? GetMessageStore<TBody>()
        {
            if (_lazyMessageStores.TryGetValue(typeof(TBody), out var lazy))
            {
                var lazyStore = (Lazy<MessageStore<TBody>>)lazy;
                return lazyStore.IsValueCreated ? lazyStore.Value : null;
            }
            return null;
        }
    }
}