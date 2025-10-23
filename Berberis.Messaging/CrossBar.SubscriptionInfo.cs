using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

partial class CrossBar
{
    /// <summary>Subscription information snapshot.</summary>
    public record struct SubscriptionInfo
    {
        /// <summary>Subscription name.</summary>
        public string Name { get; init; }
        /// <summary>True if wildcard subscription.</summary>
        public bool IsWildcard { get; init; }
        /// <summary>Channel or pattern name.</summary>
        public string ChannelName { get; init; }
        /// <summary>Subscription creation time.</summary>
        public DateTime SubscribedOn { get; init; }
        /// <summary>Message conflation interval.</summary>
        public TimeSpan ConflationInterval { get; init; }
        /// <summary>Subscription statistics.</summary>
        public StatsTracker Statistics { get; init; }
    }
}