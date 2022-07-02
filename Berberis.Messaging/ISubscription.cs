namespace Berberis.Messaging;

public interface ISubscription : IDisposable
{
    string Name { get; }
    DateTime SubscribedOn { get; }
    TimeSpan ConflationInterval { get; }
    public Task MessageLoop { get; }

    StatsTracker Statistics { get; }
}