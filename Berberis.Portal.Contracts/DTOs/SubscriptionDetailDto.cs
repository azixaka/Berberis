namespace Berberis.Portal.Contracts.DTOs;

/// <summary>Detailed subscription information with full statistics.</summary>
public class SubscriptionDetailDto : SubscriptionInfoDto
{
    /// <summary>Dequeue rate (messages/second).</summary>
    public double DequeueRate { get; set; }

    /// <summary>Total enqueued messages.</summary>
    public long TotalEnqueued { get; set; }

    /// <summary>Total dequeued messages.</summary>
    public long TotalDequeued { get; set; }

    /// <summary>Average service time (ms).</summary>
    public double AvgServiceTimeMs { get; set; }

    /// <summary>Minimum latency (ms).</summary>
    public double MinLatencyMs { get; set; }

    /// <summary>Maximum latency (ms).</summary>
    public double MaxLatencyMs { get; set; }

    /// <summary>Minimum service time (ms).</summary>
    public double MinServiceTimeMs { get; set; }

    /// <summary>Maximum service time (ms).</summary>
    public double MaxServiceTimeMs { get; set; }

    /// <summary>Percentile service time (ms).</summary>
    public double PercentileServiceTimeMs { get; set; }

    /// <summary>Average response time (latency + service time).</summary>
    public double AvgResponseTimeMs { get; set; }

    /// <summary>Latency to response time ratio.</summary>
    public double LatencyToResponseRatio { get; set; }

    /// <summary>Estimated concurrent active messages (Little's Law).</summary>
    public double EstimatedActiveMessages { get; set; }

    /// <summary>Handler timeout duration (if configured).</summary>
    public TimeSpan? HandlerTimeout { get; set; }

    /// <summary>Backpressure/slow consumer strategy.</summary>
    public string? BackpressureStrategy { get; set; }
}
