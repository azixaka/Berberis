namespace Berberis.Messaging.Exceptions;

/// <summary>
/// Exception thrown when attempting to publish or subscribe to a channel
/// with a different type than previously established.
/// </summary>
public class ChannelTypeMismatchException : InvalidOperationException
{
    /// <summary>
    /// Gets the name of the channel where the type mismatch occurred.
    /// </summary>
    public string ChannelName { get; }

    /// <summary>
    /// Gets the expected type for the channel.
    /// </summary>
    public Type ExpectedType { get; }

    /// <summary>
    /// Gets the actual type that was attempted.
    /// </summary>
    public Type ActualType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelTypeMismatchException"/> class.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="expectedType">The expected type for the channel.</param>
    /// <param name="actualType">The actual type that was attempted.</param>
    public ChannelTypeMismatchException(
        string channelName,
        Type expectedType,
        Type actualType)
        : base($"Channel '{channelName}' type mismatch: expected {expectedType.Name}, actual {actualType.Name}")
    {
        ChannelName = channelName;
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}
