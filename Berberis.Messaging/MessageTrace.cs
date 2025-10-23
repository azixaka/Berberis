namespace Berberis.Messaging;

/// <summary>Trace operation type.</summary>
public enum OpType : byte
{
    /// <summary>Channel publish.</summary>
    ChannelPublish,
    /// <summary>Subscription dequeue.</summary>
    SubscriptionDequeue,
    /// <summary>Subscription processed.</summary>
    SubscriptionProcessed
}

/// <summary>Message trace information.</summary>
public readonly struct MessageTrace
{
    /// <summary>Operation type.</summary>
    public OpType OpType { get; init; }
    /// <summary>Message key.</summary>
    public string? MessageKey { get; init; }
    /// <summary>Source identifier.</summary>
    public string? From { get; init; }
    /// <summary>Correlation ID.</summary>
    public long CorrelationId { get; init; }
    /// <summary>Channel name.</summary>
    public string Channel { get; init; }
    /// <summary>Subscription name.</summary>
    public string SubscriptionName { get; init; }
    /// <summary>Timestamp ticks.</summary>
    public long Ticks { get; init; }

    /// <summary>Returns a string representation of the message trace.</summary>
    public override string ToString() =>
        $"[{OpType}] | msgKey:{MessageKey} | from:{From} | corId:{CorrelationId} | channel:{Channel} | subNam:{SubscriptionName} | ticks:{Ticks}";
}