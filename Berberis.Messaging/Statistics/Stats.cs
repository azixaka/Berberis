namespace Berberis.Messaging.Statistics;

/// <summary>Subscription statistics snapshot.</summary>
public readonly struct Stats
{
    /// <summary>Interval window (ms).</summary>
    public readonly float IntervalMs;

    /// <summary>Dequeue rate (msg/s).</summary>
    public readonly float DequeueRate;

    /// <summary>Process rate (msg/s).</summary>
    public readonly float ProcessRate;

    /// <summary>Total enqueued messages.</summary>
    public readonly long TotalEnqueuedMessages;

    /// <summary>Total dequeued messages.</summary>
    public readonly long TotalDequeuedMessages;

    /// <summary>Total processed messages.</summary>
    public readonly long TotalProcessedMessages;

    /// <summary>Average latency (ms).</summary>
    public readonly float AvgLatencyTimeMs;

    /// <summary>Min latency (ms).</summary>
    public readonly float MinLatencyTimeMs;

    /// <summary>Max latency (ms).</summary>
    public readonly float MaxLatencyTimeMs;

    /// <summary>Percentile latency (ms).</summary>
    public readonly float PercentileLatencyTimeMs;

    /// <summary>Average service time (ms).</summary>
    public readonly float AvgServiceTimeMs;

    /// <summary>Min service time (ms).</summary>
    public readonly float MinServiceTimeMs;

    /// <summary>Max service time (ms).</summary>
    public readonly float MaxServiceTimeMs;

    /// <summary>Percentile service time (ms).</summary>
    public readonly float PercentileServiceTimeMs;

    /// <summary>Total handler timeouts.</summary>
    public readonly long NumOfTimeouts;

    /// <summary>Average response time (latency+service).</summary>
    public float AvgResponseTime { get => AvgLatencyTimeMs + AvgServiceTimeMs; }

    /// <summary>Latency/response ratio.</summary>
    public float LatencyToResponseTimeRatio { get => AvgLatencyTimeMs / (AvgLatencyTimeMs + AvgServiceTimeMs); }

    /// <summary>Conflation ratio (processed/dequeued).</summary>
    public float ConflationRatio { get => ProcessRate / DequeueRate; }

    /// <summary>Current queue length.</summary>
    public long QueueLength
    {
        get
        {
            var len = TotalEnqueuedMessages - TotalDequeuedMessages;
            return len < 0 ? 0 : len;
        }
    }

    /// <summary>Estimated active messages (Little's Law).</summary>
    public float EstimatedAvgActiveMessages { get => ProcessRate * (AvgLatencyTimeMs + AvgServiceTimeMs) / 1000.0f; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Stats"/> struct.
    /// </summary>
    public Stats(float intervalMs,
        float dequeueRate,
        float processRate,
        long totalEnqueuedMessages,
        long totalDequeuedMessages,
        long totalProcessedMessages,
        float avgLatencyTimeMs,
        float avgServiceTimeMs,
        float percentileLatencyTimeMs,
        float percentileServiceTimeMs,
        float minLatencyTimeMs,
        float maxLatencyTimeMs,
        float minServiceTimeMs,
        float maxServiceTimeMs,
        long numOfTimeouts = 0
        )
    {
        IntervalMs = intervalMs;
        DequeueRate = dequeueRate;
        ProcessRate = processRate;
        TotalEnqueuedMessages = totalEnqueuedMessages;
        TotalDequeuedMessages = totalDequeuedMessages;
        TotalProcessedMessages = totalProcessedMessages;
        AvgLatencyTimeMs = avgLatencyTimeMs;
        AvgServiceTimeMs = avgServiceTimeMs;
        PercentileLatencyTimeMs = percentileLatencyTimeMs;
        PercentileServiceTimeMs = percentileServiceTimeMs;
        MinLatencyTimeMs = minLatencyTimeMs;
        MaxLatencyTimeMs = maxLatencyTimeMs;
        MinServiceTimeMs = minServiceTimeMs;
        MaxServiceTimeMs = maxServiceTimeMs;
        NumOfTimeouts = numOfTimeouts;
    }
}
