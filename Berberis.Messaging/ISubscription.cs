namespace Berberis.Messaging;

public interface ISubscription : IDisposable
{
    long Id { get; }

    Task RunReadLoopAsync(CancellationToken token = default);

    StatsTracker Statistics { get; }
}