namespace Berberis.Messaging;

partial class CrossBar
{
    public record struct SubscriptionInfo
    {
        public string Name { get; init; }
        public DateTime SubscribedOn { get; init; }
        public TimeSpan ConflationInterval { get; init; }
        public StatsTracker Statistics { get; init; }
    }
}
