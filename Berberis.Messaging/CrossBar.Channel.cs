using System.Collections.Concurrent;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal sealed record Channel
    {
        private long _channelSequenceId;

        private bool _messageStoreInitialised;
        private IMessageStore _messageStore;

        public long NextMessageId() => Interlocked.Increment(ref _channelSequenceId);

        public Type BodyType { get; init; }

        public string Name { get; init; }

        public ConcurrentDictionary<long, ISubscription> Subscriptions { get; } = new();

        public ChannelStatsTracker Statistics { get; } = new ();

        public DateTime LastPublishedAt { get; internal set; }

        public string? LastPublishedBy { get; internal set; }

        public MessageStore<TBody>? GetMessageStore<TBody>()
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