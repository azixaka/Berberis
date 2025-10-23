namespace Berberis.Messaging.Exceptions;

/// <summary>
/// Exception thrown when a channel name is invalid.
/// </summary>
public class InvalidChannelNameException : ArgumentException
{
    /// <summary>
    /// Gets the invalid channel name that caused the exception.
    /// </summary>
    public string? ChannelName { get; }

    /// <summary>
    /// Gets the specific reason why the channel name is invalid.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidChannelNameException"/> class.
    /// </summary>
    /// <param name="channelName">The invalid channel name.</param>
    /// <param name="reason">The reason why the channel name is invalid.</param>
    public InvalidChannelNameException(string? channelName, string reason)
        : base($"Invalid channel name '{channelName}': {reason}", nameof(channelName))
    {
        ChannelName = channelName;
        Reason = reason;
    }
}
