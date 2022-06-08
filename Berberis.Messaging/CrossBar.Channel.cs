using System.Collections.Concurrent;

namespace Berberis.Messaging;

partial class CrossBar
{
	internal record Channel
	{
		private long _channelSequenceId;

		public long NextMessageId() => Interlocked.Increment(ref _channelSequenceId);

		public Type BodyType { get; init; }
		
		public ConcurrentDictionary<long, ISubscription> Subscriptions { get; }
			= new ConcurrentDictionary<long, ISubscription>();

		public StatsTracker Statistics { get; } = new StatsTracker();
	}
}
