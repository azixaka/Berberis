using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

partial class CrossBar
{
    /// <summary>Channel information snapshot.</summary>
    public record struct ChannelInfo
    {
        /// <summary>Channel name.</summary>
        public required string Name { get; init; }
        /// <summary>Message body type.</summary>
        public required Type BodyType { get; init; }
        /// <summary>Channel statistics.</summary>
        public ChannelStatsTracker Statistics { get; init; }
        /// <summary>Last publish time.</summary>
        public DateTime LastPublishedAt { get; init; }
        /// <summary>Last publisher identifier.</summary>
        public string? LastPublishedBy { get; init; }
    }
}
