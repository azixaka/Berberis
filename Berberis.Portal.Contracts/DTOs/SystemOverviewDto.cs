namespace Berberis.Portal.Contracts.DTOs;

/// <summary>System-wide overview of CrossBar state.</summary>
public class SystemOverviewDto
{
    /// <summary>Total number of channels.</summary>
    public int TotalChannels { get; set; }

    /// <summary>Total number of subscriptions.</summary>
    public int TotalSubscriptions { get; set; }

    /// <summary>Total number of wildcard subscriptions.</summary>
    public int WildcardSubscriptions { get; set; }

    /// <summary>Aggregate message throughput (messages/second).</summary>
    public double SystemThroughput { get; set; }

    /// <summary>Total messages published across all channels.</summary>
    public long TotalMessagesPublished { get; set; }

    /// <summary>Total messages processed across all subscriptions.</summary>
    public long TotalMessagesProcessed { get; set; }

    /// <summary>Total handler timeouts across all subscriptions.</summary>
    public long TotalTimeouts { get; set; }

    /// <summary>Number of subscriptions with queue depth > 0.</summary>
    public int SubscriptionsWithBacklog { get; set; }
}
