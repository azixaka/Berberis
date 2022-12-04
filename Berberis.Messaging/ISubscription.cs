using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

public interface ISubscription : IDisposable
{
    string Name { get; }
    bool IsWildcard { get; }
    string ChannelName { get; }
    DateTime SubscribedOn { get; }
    TimeSpan ConflationInterval { get; }
    Task MessageLoop { get; }
    Type MessageBodyType { get; }

    bool IsDetached { get; set; }
    bool IsProcessingSuspended { get; set; }

    StatsTracker Statistics { get; }
}