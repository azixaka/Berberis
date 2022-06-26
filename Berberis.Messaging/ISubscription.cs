namespace Berberis.Messaging;

public interface ISubscription : IDisposable
{
    long Id { get; }
    string Name { get; }

    Task RunReadLoopAsync(CancellationToken token = default);

    StatsTracker Statistics { get; }
}