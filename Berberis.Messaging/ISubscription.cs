using Berberis.Messaging.Statistics;

namespace Berberis.Messaging;

/// <summary>Subscription interface.</summary>
public interface ISubscription : IDisposable
{
    /// <summary>Subscription name.</summary>
    string Name { get; }
    /// <summary>True if wildcard subscription.</summary>
    bool IsWildcard { get; }
    /// <summary>Channel or pattern name.</summary>
    string ChannelName { get; }
    /// <summary>Subscription creation time.</summary>
    DateTime SubscribedOn { get; }
    /// <summary>Message conflation interval.</summary>
    TimeSpan ConflationInterval { get; }
    /// <summary>Message processing loop task.</summary>
    Task MessageLoop { get; }
    /// <summary>Message body type.</summary>
    Type MessageBodyType { get; }

    /// <summary>True if detached from channel.</summary>
    bool IsDetached { get; set; }
    /// <summary>Suspend/resume message processing.</summary>
    bool IsProcessingSuspended { get; set; }

    /// <summary>Performance statistics tracker.</summary>
    StatsTracker Statistics { get; }

    /// <summary>Gets handler timeout count.</summary>
    long GetTimeoutCount();

    /// <summary>Notifies subscription that its channel is being deleted.</summary>
    void NotifyChannelDeletion();
}