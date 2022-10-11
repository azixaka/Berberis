namespace Berberis.Messaging;

public readonly struct ChannelStats
{
    /// <summary>
    /// Total inter-publish time in milliseconds
    /// </summary>
    private readonly float _totalInterPublishTimeMs;

    /// <summary>
    /// Interval window in milliseconds for which these calculation were made
    /// </summary>
    public readonly float IntervalMs;

    /// <summary>
    /// Message rate observed in this interval, in msg/s
    /// </summary>
    public readonly float PublishRateInterval;

    /// <summary>
    /// Total number of messages observed in this interval
    /// </summary>
    public readonly long TotalMessages;

    /// <summary>
    /// All time Publish rate, in msg/s
    /// </summary>
    public float PublishRateLongTerm => (TotalMessages / _totalInterPublishTimeMs) * 1000;

    public ChannelStats(float intervalMs,
        float messagesPerSecond,
        float totalInterPublishTimeMs,
        long totalMessages)
    {
        IntervalMs = intervalMs;
        PublishRateInterval = messagesPerSecond;
        _totalInterPublishTimeMs = totalInterPublishTimeMs;
        TotalMessages = totalMessages;
    }

    public override string ToString() => $"Int: {IntervalMs:N0} ms; Int Rate: {PublishRateInterval:N1} msg/s; LT Rate: {PublishRateLongTerm:N1} msg/s; Total: {TotalMessages:N0}";
}
