namespace Berberis.Messaging;

partial class CrossBar
{
    public record struct SubscriptionInfo
    {
        public long Id { get; init; }
        public StatsTracker Statistics { get; init; }
    }
}
