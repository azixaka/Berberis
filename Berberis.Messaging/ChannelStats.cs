namespace Berberis.Messaging;

public readonly struct ChannelStats
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
    /// Total inter-publish time in milliseconds
    /// </summary>
    public readonly float TotalInterPublishTimeMs;

    /// <summary>
    /// Total number of messages observed in this interval
    /// </summary>
    public readonly long TotalMessages;

    /// <summary>
    /// All time Publish rate, in msg/s
    /// </summary>
    public float PublishRate { get => (TotalMessages / TotalInterPublishTimeMs) * 1000; }

    public ChannelStats(float intervalMs,
        float messagesPerSecond,
        float totalInterPublishTimeMs,
        long totalMessages)
    {
        IntervalMs = intervalMs;
        MessagesPerSecond = messagesPerSecond;
        TotalInterPublishTimeMs = totalInterPublishTimeMs;
        TotalMessages = totalMessages;
    }

    public override string ToString() => $"Int: {IntervalMs:N0} ms; Int Rate: {MessagesPerSecond:N1} msg/s; Avg Rate: {PublishRate:N1} msg/s; Total: {TotalMessages:N0}";
}
