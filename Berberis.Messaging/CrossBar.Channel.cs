using System.Collections.Concurrent;
using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal record Channel
    {
        private long _channelSequenceId;

        private readonly object _messageStoreLock = new();
        private IMessageStore? _messageStore;

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
            // Type safety check (defensive, negligible cost ~1ns)
            if (typeof(TBody) != BodyType)
                throw new InvalidOperationException($"MessageStore type {typeof(TBody).Name} doesn't match channel type {BodyType.Name}");

            // Hot path - check without lock (allocation-free after initialization)
            var store = Volatile.Read(ref _messageStore);
            if (store != null)
                return (MessageStore<TBody>)store;

            // Cold path - double-checked locking for thread-safe initialization
            lock (_messageStoreLock)
            {
                if (_messageStore == null)
                    _messageStore = new MessageStore<TBody>();

                return (MessageStore<TBody>)_messageStore;
            }
        }

        public MessageStore<TBody>? GetMessageStore<TBody>()
        {
            var store = Volatile.Read(ref _messageStore);
            return store as MessageStore<TBody>;
        }

        public int GetStoredMessageCount()
        {
            var store = Volatile.Read(ref _messageStore);
            return store?.Count ?? 0;
        }
    }
}