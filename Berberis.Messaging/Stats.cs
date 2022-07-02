namespace Berberis.Messaging;

public readonly struct Stats
{
    /// <summary>
    /// Interval window in milliseconds for which these calculation were made
    /// </summary>
    public readonly float IntervalMs;

    /// <summary>
    /// Enqueue message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float MessagesPerSecondEnqueue;

    /// <summary>
    /// Dequeue message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float MessagesPerSecondDequeued;

    /// <summary>
    /// Processing message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float MessagesPerSecondProcessed;

    /// <summary>
    /// Total number of enqueued messages observed
    /// </summary>
    public readonly long TotalEnqueuedMessages;

    /// <summary>
    /// Total number of dequeued messages
    /// </summary>
    public readonly long TotalDequeuedMessages;

    /// <summary>
    /// Total message inter-dequeue time in milliseconds
    /// </summary>
    public readonly float TotalInterDequeueTimeMs;

    /// <summary>
    /// Total number of processed messages
    /// </summary>
    public readonly long TotalProcessedMessages;

    /// <summary>
    /// Total message inter-process time in milliseconds
    /// </summary>
    public readonly float TotalInterProcessTimeMs;

    /// <summary>
    /// Total latency time in milliseconds
    /// </summary>
    public readonly float TotalLatencyTimeMs;

    /// <summary>
    /// Average latency time in this interval, in milliseconds, i.e how long it took on average to wait to be processed
    /// </summary>
    public readonly float AvgLatencyTimeMs;

    /// <summary>
    /// Total latency time in milliseconds
    /// </summary>
    public readonly float TotalServiceTimeMs;

    /// <summary>
    /// Average service time in this interval, in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float AvgServiceTimeMs;

    /// <summary>
    /// Processed to Dequeued messages ratio
    /// </summary>
    public float ConflationRate { get => TotalProcessedMessages / (float)TotalDequeuedMessages; }

    /// <summary>
    /// Current queue length
    /// </summary>
    public long QueueLength
    {
        get
        {
            var len = TotalDequeuedMessages - TotalEnqueuedMessages;
            return len < 0 ? 0 : len;
        }
    }

    /// <summary>
    /// Latency to Response time (latency + service time) ratio
    /// </summary>
    public float LatencyToResponseTimeRatio { get => AvgAllLatencyTimeMs / (AvgAllLatencyTimeMs + AvgAllServiceTimeMs); }

    /// <summary>
    /// All time Dequeue rate, in msg/s
    /// </summary>
    public float DequeueRate { get => (TotalDequeuedMessages / TotalInterDequeueTimeMs) * 1000; }

    /// <summary>
    /// All time average latency time in ms
    /// </summary>
    public float AvgAllLatencyTimeMs { get => TotalLatencyTimeMs / TotalDequeuedMessages; }

    /// <summary>
    /// Estimated average active number of messages in the system as per Little's Law, at the dequeuing point
    /// </summary>
    public float EstimatedAvgActiveMessagesDequeue { get => DequeueRate * (AvgAllLatencyTimeMs + AvgAllServiceTimeMs) / 1000.0f; }

    /// <summary>
    /// All time Process rate, in msg/s
    /// </summary>
    public float ProcessRate { get => (TotalProcessedMessages / TotalInterProcessTimeMs) * 1000; }

    /// <summary>
    /// All time average service time in ms
    /// </summary>
    public float AvgAllServiceTimeMs { get => TotalServiceTimeMs / TotalProcessedMessages; }

    /// <summary>
    /// Estimated average active number of messages in the system as per Little's Law, at the processing point
    /// </summary>
    public float EstimatedAvgActiveMessagesProcess { get => ProcessRate * (AvgAllLatencyTimeMs + AvgAllServiceTimeMs) / 1000.0f; }

    public Stats(float intervalMs,
        float messagesPerSecondIn,
        float messagesPerSecondDequeued,
        float messagesPerSecondProcessed,
        long totalMessagesIn,
        long totalDequeuedMessages,
        float totalInterDequeueTimeMs,
        long totalMessagesProcessed,
        float totalInterProcessTimeMs,
        float totalLatencyTimeMs,
        float avgLatencyTimeMs,
        float totalServiceTimeMs,
        float avgServiceTimeMs)
    {
        IntervalMs = intervalMs;
        MessagesPerSecondEnqueue = messagesPerSecondIn;
        MessagesPerSecondDequeued = messagesPerSecondDequeued;
        MessagesPerSecondProcessed = messagesPerSecondProcessed;
        TotalEnqueuedMessages = totalMessagesIn;
        TotalDequeuedMessages = totalDequeuedMessages;
        TotalInterDequeueTimeMs = totalInterDequeueTimeMs;
        TotalProcessedMessages = totalMessagesProcessed;
        TotalInterProcessTimeMs = totalInterProcessTimeMs;
        TotalLatencyTimeMs = totalLatencyTimeMs;
        AvgLatencyTimeMs = avgLatencyTimeMs;
        TotalServiceTimeMs = totalServiceTimeMs;
        AvgServiceTimeMs = avgServiceTimeMs;
    }
}
