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
    public readonly float PublishRate;

    /// <summary>
    /// Total number of messages observed in this interval
    /// </summary>
    public readonly long TotalMessages;

    public ChannelStats(float intervalMs,
        float messagesPerSecond,
        long totalMessages)
    {
        IntervalMs = intervalMs;
        PublishRate = messagesPerSecond;
        TotalMessages = totalMessages;
    }

    public override string ToString() => $"Int: {IntervalMs:N0} ms; Rate: {PublishRate:N1} msg/s; Total: {TotalMessages:N0}";
}
