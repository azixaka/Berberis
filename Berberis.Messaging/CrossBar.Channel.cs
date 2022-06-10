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

        public ConcurrentDictionary<long, ISubscription> Subscriptions { get; }
            = new ConcurrentDictionary<long, ISubscription>();

        public StatsTracker Statistics { get; } = new StatsTracker();

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