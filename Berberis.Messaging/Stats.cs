namespace Berberis.Messaging;

public readonly struct Stats
{
    /// <summary>
    /// Interval window in milliseconds for which these calculation were made
    /// </summary>
    public readonly float IntervalMs;

    /// <summary>
    /// Incoming message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float MessagesPerSecondIn;

    /// <summary>
    /// Processing message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float MessagesPerSecondOut;

    /// <summary>
    /// Total number of incoming messages observed in this interval
    /// </summary>
    public readonly long TotalMessagesIn;

    /// <summary>
    /// Total number of processed messages observed in this interval
    /// </summary>
    public readonly long TotalMessagesOut;

    /// <summary>
    /// Average latency time in milliseconds, i.e how long it took on average to wait to be processed
    /// </summary>
    public readonly float AvgLatencyTime;

    /// <summary>
    /// Average service time in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float AvgServiceTime;

    public Stats(float intervalMs,
        float messagesPerSecondIn,
        float messagesPerSecondOut,
        long totalMessagesIn,
        long totalMessagesOut,
        float avgLatencyTime,
        float avgServiceTime)
    {
        IntervalMs = intervalMs;
        MessagesPerSecondIn = messagesPerSecondIn;
        MessagesPerSecondOut = messagesPerSecondOut;
        TotalMessagesIn = totalMessagesIn;
        TotalMessagesOut = totalMessagesOut;
        AvgLatencyTime = avgLatencyTime;
        AvgServiceTime = avgServiceTime;
    }

    public override string ToString() =>
        $"Interval: {IntervalMs:N2}ms; In: {MessagesPerSecondIn:N2} msg/s; Out: {MessagesPerSecondOut:N2} msg/s; Total In: {TotalMessagesIn:N}; Total Out: {TotalMessagesOut:N}; Avg Latency: {AvgLatencyTime:N2} ms; Avg Svc: {AvgServiceTime:N2} ms";
}
