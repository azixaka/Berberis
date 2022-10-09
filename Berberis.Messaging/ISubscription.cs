namespace Berberis.Messaging;

public interface ISubscription : IDisposable
{
    string Name { get; }
    bool IsWildcard { get; }
    DateTime SubscribedOn { get; }
    TimeSpan ConflationInterval { get; }
    Task MessageLoop { get; }
    Type MessageBodyType { get; }

    StatsTracker Statistics { get; }
}