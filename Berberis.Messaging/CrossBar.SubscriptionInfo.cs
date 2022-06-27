namespace Berberis.Messaging;

partial class CrossBar
{
    public record struct SubscriptionInfo
    {
        public string Name { get; init; }
        public StatsTracker Statistics { get; init; }
    }
}
