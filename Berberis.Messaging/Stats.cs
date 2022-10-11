namespace Berberis.Messaging;

public readonly struct Stats
{
    /// <summary>
    /// Total message inter-dequeue time in milliseconds
    /// </summary>
    private readonly float _totalInterDequeueTimeMs;

    /// <summary>
    /// Total message inter-process time in milliseconds
    /// </summary>
    private readonly float _totalInterProcessTimeMs;

    /// <summary>
    /// Total latency time in milliseconds
    /// </summary>
    private readonly float _totalLatencyTimeMs;

    /// <summary>
    /// Total latency time in milliseconds
    /// </summary>
    private readonly float _totalServiceTimeMs;

    /// <summary>
    /// Interval window in milliseconds for which these calculation were made
    /// </summary>
    public readonly float IntervalMs;

    /// <summary>
    /// Enqueue message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float EnqueueRateInterval;

    /// <summary>
    /// Dequeue message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float DequeueRateInterval;

    /// <summary>
    /// Processing message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float ProcessRateInterval;

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
    public readonly float AvgLatencyTimeMsInterval;

    /// <summary>
    /// Average service time in this interval, in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float AvgServiceTimeMsInterval;

    /// <summary>
    /// Processed to Dequeued messages ratio
    /// </summary>
    public float ConflationRatioLongTerm => TotalProcessedMessages / (float)TotalDequeuedMessages;

    /// <summary>
    /// Current queue length
    /// </summary>
    public long QueueLength
    {
        get
        {
            var len = TotalEnqueuedMessages - TotalDequeuedMessages;
            return Math.Max(0, len);//len < 0 ? 0 : len
        }
    }

    /// <summary>
    /// Latency to Response time (latency + service time) ratio
    /// </summary>
    public float LatencyToResponseTimeRatioLongTerm => AvgLatencyTimeMsLongTerm / (AvgLatencyTimeMsLongTerm + AvgServiceTimeMsLongTerm);

    /// <summary>
    /// All time Dequeue rate, in msg/s
    /// </summary>
    public float DequeueRateLongTerm => (TotalDequeuedMessages / _totalInterDequeueTimeMs) * 1000;

    /// <summary>
    /// All time average latency time in ms
    /// </summary>
    public float AvgLatencyTimeMsLongTerm => _totalLatencyTimeMs / TotalDequeuedMessages;

    /// <summary>
    /// All time Process rate, in msg/s
    /// </summary>
    public float ProcessRateLongTerm => (TotalProcessedMessages / _totalInterProcessTimeMs) * 1000;

    /// <summary>
    /// All time average service time in ms
    /// </summary>
    public float AvgServiceTimeMsLongTerm => _totalServiceTimeMs / TotalProcessedMessages;

    /// <summary>
    /// Estimated average active number of messages in the system as per Little's Law, at the processing point
    /// </summary>
    public float EstimatedAvgActiveMessages => ProcessRateLongTerm * (AvgLatencyTimeMsLongTerm + AvgServiceTimeMsLongTerm) / 1000.0f;

    public Stats(float intervalMs,
        float enqueueRateInterval,
        float dequeueRateInterval,
        float processRateInterval,
        long totalEnqueuedMessages,
        long totalDequeuedMessages,
        float totalInterDequeueTimeMs,
        long totalProcessedMessages,
        float totalInterProcessTimeMs,
        float totalLatencyTimeMs,
        float avgLatencyTimeMsInterval,
        float totalServiceTimeMs,
        float avgServiceTimeMsInterval)
    {
        IntervalMs = intervalMs;
        EnqueueRateInterval = enqueueRateInterval;
        DequeueRateInterval = dequeueRateInterval;
        ProcessRateInterval = processRateInterval;
        TotalEnqueuedMessages = totalEnqueuedMessages;
        TotalDequeuedMessages = totalDequeuedMessages;
        _totalInterDequeueTimeMs = totalInterDequeueTimeMs;
        TotalProcessedMessages = totalProcessedMessages;
        _totalInterProcessTimeMs = totalInterProcessTimeMs;
        _totalLatencyTimeMs = totalLatencyTimeMs;
        AvgLatencyTimeMsInterval = avgLatencyTimeMsInterval;
        _totalServiceTimeMs = totalServiceTimeMs;
        AvgServiceTimeMsInterval = avgServiceTimeMsInterval;
    }
}
