namespace Berberis.Messaging.Recorder;

/// <summary>
/// Statistics about recording performance and throughput.
/// </summary>
/// <remarks>
/// All timing measurements include the complete message processing pipeline:
/// message parsing from the internal buffer AND stream write I/O operations.
/// This provides accurate end-to-end performance metrics for the recording system.
/// </remarks>
public readonly struct RecorderStats
{
    /// <summary>
    /// Interval window in milliseconds for which these calculation were made
    /// </summary>
    public readonly float IntervalMs;

    /// <summary>
    /// Message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float MessagesPerSecond;

    /// <summary>
    /// Total number of messages observed in this interval
    /// </summary>
    public readonly float TotalMessages;

    /// <summary>
    /// Bandwidth observed in this interval, in b/s
    /// </summary>
    public readonly float BytesPerSecond;

    /// <summary>
    /// Total number of bytes observed in this interval
    /// </summary>
    public readonly float TotalBytes;

    /// <summary>
    /// Average service time in milliseconds, i.e how long it took on average to process an operation
    /// </summary>
    public readonly float AvgServiceTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecorderStats"/> struct.
    /// </summary>
    public RecorderStats(float intervalMs, float messagesPerSecond, float totalMessages, float bytesPerSecond, float totalBytes, float avgServiceTime)
    {
        IntervalMs = intervalMs;
        MessagesPerSecond = messagesPerSecond;
        TotalMessages = totalMessages;
        BytesPerSecond = bytesPerSecond;
        TotalBytes = totalBytes;
        AvgServiceTime = avgServiceTime;
    }
}
