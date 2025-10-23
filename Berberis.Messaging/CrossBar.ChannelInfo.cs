using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

partial class CrossBar
{
    public record struct ChannelInfo
    {
        public required string Name { get; init; }
        public required Type BodyType { get; init; }
        public ChannelStatsTracker Statistics { get; init; }
        public DateTime LastPublishedAt { get; init; }
        public string? LastPublishedBy { get; init; }
    }
}
