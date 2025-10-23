namespace Berberis.Messaging.Exceptions;

/// <summary>
/// Exception thrown when a message handler exceeds its configured timeout.
/// </summary>
public class HandlerTimeoutException : TimeoutException
{
    /// <summary>
    /// Gets the name of the subscription where the timeout occurred.
    /// </summary>
    public string SubscriptionName { get; }

    /// <summary>
    /// Gets the name of the channel where the timeout occurred.
    /// </summary>
    public string ChannelName { get; }

    /// <summary>
    /// Gets the ID of the message that timed out.
    /// </summary>
    public long MessageId { get; }

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerTimeoutException"/> class.
    /// </summary>
    /// <param name="subscriptionName">The name of the subscription.</param>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="messageId">The ID of the message.</param>
    /// <param name="timeout">The timeout duration.</param>
    public HandlerTimeoutException(
        string subscriptionName,
        string channelName,
        long messageId,
        TimeSpan timeout)
        : base($"Handler for subscription '{subscriptionName}' on channel '{channelName}' " +
               $"timed out after {timeout.TotalMilliseconds}ms processing message {messageId}")
    {
        SubscriptionName = subscriptionName;
        ChannelName = channelName;
        MessageId = messageId;
        Timeout = timeout;
    }
}
