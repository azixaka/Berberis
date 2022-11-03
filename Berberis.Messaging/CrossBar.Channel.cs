using System.Collections.Concurrent;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal record Channel
    {
        private long _channelSequenceId;

        private bool _messageStoreInitialised;
        private IMessageStore _messageStore;

        public long NextMessageId() => Interlocked.Increment(ref _channelSequenceId);

        public Type BodyType { get; init; }

        public string Name { get; init; }

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

        public MessageStore<TBody> GetMessageStore<TBody>()
        {
            if (Volatile.Read(ref _messageStoreInitialised))
            {
                return _messageStore as MessageStore<TBody>;
            }

            var store = new MessageStore<TBody>();
            _messageStore = store;
            Volatile.Write(ref _messageStoreInitialised, true);
            return store;
        }
    }
}