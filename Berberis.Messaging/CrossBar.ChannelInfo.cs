namespace Berberis.Messaging;

partial class CrossBar
{
    public record struct ChannelInfo
    {
        public string Name { get; init; }
        public Type BodyType { get; init; }
        public ChannelStatsTracker Statistics { get; init; }
    }
}
