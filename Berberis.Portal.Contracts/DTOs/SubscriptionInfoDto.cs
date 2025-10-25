namespace Berberis.Portal.Contracts.DTOs;

/// <summary>Subscription information for API responses.</summary>
public class SubscriptionInfoDto
{
    /// <summary>Subscription identifier (name or ID).</summary>
    public required string Id { get; set; }

    /// <summary>Channel name or pattern.</summary>
    public required string ChannelPattern { get; set; }

    /// <summary>True if this is a wildcard subscription.</summary>
    public bool IsWildcard { get; set; }

    /// <summary>Subscription status (Active, Suspended, Detached).</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Current queue depth.</summary>
    public long QueueDepth { get; set; }

    /// <summary>Process rate (messages/second).</summary>
    public double ProcessRate { get; set; }

    /// <summary>Average latency (ms).</summary>
    public double AvgLatencyMs { get; set; }

    /// <summary>Percentile latency (ms).</summary>
    public double PercentileLatencyMs { get; set; }

    /// <summary>Total processed messages.</summary>
    public long TotalProcessed { get; set; }

    /// <summary>Total handler timeouts.</summary>
    public long TimeoutCount { get; set; }

    /// <summary>Subscription created timestamp.</summary>
    public DateTime SubscribedOn { get; set; }

    /// <summary>Conflation interval (if any).</summary>
    public TimeSpan? ConflationInterval { get; set; }

    /// <summary>Conflation effectiveness ratio (0.0-1.0).</summary>
    public double? ConflationRatio { get; set; }
}
