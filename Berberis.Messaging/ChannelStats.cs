namespace Berberis.Messaging;

public readonly struct ChannelStats
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

    public ChannelStats(float intervalMs,
        float messagesPerSecondIn,
        float messagesPerSecondOut,
        long totalMessagesIn,
        long totalMessagesOut)
    {
        IntervalMs = intervalMs;
        MessagesPerSecondIn = messagesPerSecondIn;
        MessagesPerSecondOut = messagesPerSecondOut;
        TotalMessagesIn = totalMessagesIn;
        TotalMessagesOut = totalMessagesOut;
    }

    public override string ToString() =>
        $"Int: {IntervalMs:N0} ms; In: {MessagesPerSecondIn:N1} msg/s; Out: {MessagesPerSecondOut:N1} msg/s; Total In: {TotalMessagesIn:N0}; Total Out: {TotalMessagesOut:N0}";
}
