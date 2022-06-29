namespace Berberis.Messaging;

public interface ISubscription : IDisposable
{
    long Id { get; }
    string Name { get; }
    DateTime SubscribedOn { get; }
    TimeSpan ConflationInterval { get; }

    Task RunReadLoopAsync(CancellationToken token = default);

    StatsTracker Statistics { get; }
}