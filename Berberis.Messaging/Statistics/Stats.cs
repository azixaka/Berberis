namespace Berberis.Messaging.Statistics;

public readonly struct Stats
{
    /// <summary>
    /// Interval window in milliseconds for which these calculation were made
    /// </summary>
    public readonly float IntervalMs;

    /// <summary>
    /// Dequeue message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float DequeueRate;

    /// <summary>
    /// Processing message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float ProcessRate;

    /// <summary>
    /// Total number of enqueued messages observed
    /// </summary>
    public readonly long TotalEnqueuedMessages;

    /// <summary>
    /// Total number of dequeued messages
    /// </summary>
    public readonly long TotalDequeuedMessages;

    /// <summary>
    /// Total number of processed messages
    /// </summary>
    public readonly long TotalProcessedMessages;

    /// <summary>
    /// Average latency time in this interval, in milliseconds, i.e how long it took on average to wait to be processed
    /// </summary>
    public readonly float AvgLatencyTimeMs;

    /// <summary>
    /// Min latency time in this interval, in milliseconds, i.e how long it took on average to wait to be processed
    /// </summary>
    public readonly float MinLatencyTimeMs;

    /// <summary>
    /// Max latency time in this interval, in milliseconds, i.e how long it took on average to wait to be processed
    /// </summary>
    public readonly float MaxLatencyTimeMs;

    /// <summary>
    /// N-th percentile latency time in milliseconds, i.e how long it took on average to wait to be processed
    /// </summary>
    public readonly float PercentileLatencyTimeMs;

    /// <summary>
    /// Average service time in this interval, in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float AvgServiceTimeMs;

    /// <summary>
    /// Min service time in this interval, in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float MinServiceTimeMs;

    /// <summary>
    /// Max service time in this interval, in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float MaxServiceTimeMs;

    /// <summary>
    /// N-th percentile service time in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float PercentileServiceTimeMs;

    /// <summary>
    /// Total number of handler timeouts
    /// </summary>
    public readonly long NumOfTimeouts;

    /// <summary>
    /// Average response time (latency + service time)
    /// </summary>
    public float AvgResponseTime { get => AvgLatencyTimeMs + AvgServiceTimeMs; }

    /// <summary>
    /// Latency to Response time (latency + service time) ratio
    /// </summary>
    public float LatencyToResponseTimeRatio { get => AvgLatencyTimeMs / (AvgLatencyTimeMs + AvgServiceTimeMs); }

    /// <summary>
    /// Processed to Dequeued messages ratio
    /// </summary>
    public float ConflationRatio { get => ProcessRate / DequeueRate; }

    /// <summary>
    /// Current queue length
    /// </summary>
    public long QueueLength
    {
        get
        {
            var len = TotalEnqueuedMessages - TotalDequeuedMessages;
            return len < 0 ? 0 : len;
        }
    }

    /// <summary>
    /// Estimated average active number of messages in the system as per Little's Law, at the processing point
    /// </summary>
    public float EstimatedAvgActiveMessages { get => ProcessRate * (AvgLatencyTimeMs + AvgServiceTimeMs) / 1000.0f; }

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
