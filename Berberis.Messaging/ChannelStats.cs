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
    /// Total number of messages observed in this interval
    /// </summary>
    public readonly long TotalMessages;

    public ChannelStats(float intervalMs,
        float messagesPerSecond,
        long totalMessages)
    {
        IntervalMs = intervalMs;
        MessagesPerSecond = messagesPerSecond;
        TotalMessages = totalMessages;
    }

    public override string ToString() => $"Int: {IntervalMs:N0} ms; Rate: {MessagesPerSecond:N1} msg/s; Total: {TotalMessages:N0}";
}
