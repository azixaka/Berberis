namespace Berberis.Messaging;

/// <summary>Lifecycle event type.</summary>
public enum LifecycleEventType : byte
{
    /// <summary>Channel created.</summary>
    ChannelCreated = 0,
    /// <summary>Channel deleted.</summary>
    ChannelDeleted = 1,
    /// <summary>Subscription created.</summary>
    SubscriptionCreated = 2,
    /// <summary>Subscription disposed.</summary>
    SubscriptionDisposed = 3
}

/// <summary>Channel and subscription lifecycle event.</summary>
public readonly struct LifecycleEvent
{
    /// <summary>Event type.</summary>
    public LifecycleEventType EventType { get; init; }
    /// <summary>Channel name.</summary>
    public string ChannelName { get; init; }
    /// <summary>Subscription name (null for channel events).</summary>
    public string? SubscriptionName { get; init; }
    /// <summary>Message body type name.</summary>
    public string MessageBodyType { get; init; }
    /// <summary>Event timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Returns a string representation of the lifecycle event.</summary>
    public override string ToString() =>
        $"[{EventType}] | channel:{ChannelName} | sub:{SubscriptionName} | type:{MessageBodyType} | time:{Timestamp:O}";
}
