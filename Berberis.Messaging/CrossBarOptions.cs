namespace Berberis.Messaging;

/// <summary>
/// Configuration options for CrossBar message broker.
/// </summary>
/// <remarks>
/// Provides centralized defaults and system-wide settings.
/// All properties have sensible defaults and can be overridden per-subscription where applicable.
/// </remarks>
public class CrossBarOptions
{
    /// <summary>
    /// Default buffer capacity for subscriptions.
    /// </summary>
    /// <remarks>
    /// This value is used when Subscribe() is called without specifying bufferCapacity.
    /// Null means unbounded (default behavior - preserves original Berberis semantics).
    /// Set to a specific value to enable bounded channels with backpressure handling.
    /// </remarks>
    public int? DefaultBufferCapacity { get; set; } = null;

    /// <summary>
    /// Default slow consumer strategy for subscriptions.
    /// </summary>
    /// <remarks>
    /// Determines behavior when a subscription's buffer is full.
    /// - SkipUpdates: Drop new messages
    /// - DropOldest: Drop oldest messages
    /// - Block: Block publisher (degrades performance)
    /// - FailSubscription: Fail the subscription
    /// </remarks>
    public SlowConsumerStrategy DefaultSlowConsumerStrategy { get; set; } = SlowConsumerStrategy.SkipUpdates;

    /// <summary>
    /// Default conflation interval for subscriptions.
    /// </summary>
    /// <remarks>
    /// When set to non-zero, messages are conflated (merged) within this time window.
    /// Zero means no conflation (deliver all messages).
    /// </remarks>
    public TimeSpan DefaultConflationInterval { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Maximum number of channels allowed.
    /// </summary>
    /// <remarks>
    /// Null means unlimited. Set this to prevent unbounded channel creation.
    /// Useful for protecting against channel name explosion in production.
    /// </remarks>
    public int? MaxChannels { get; set; } = null;

    /// <summary>
    /// Maximum allowed length for channel names.
    /// </summary>
    /// <remarks>
    /// Channel names exceeding this length will be rejected.
    /// </remarks>
    public int MaxChannelNameLength { get; set; } = 256;

    /// <summary>
    /// Enable message tracing to system tracing channel.
    /// </summary>
    /// <remarks>
    /// When enabled, all published messages are traced to the system channel.
    /// Useful for debugging and observability but adds overhead.
    /// </remarks>
    public bool EnableMessageTracing { get; set; } = false;

    /// <summary>
    /// Enable lifecycle event tracking to system lifecycle channel.
    /// </summary>
    /// <remarks>
    /// When enabled, channel and subscription creation/deletion events are published to the system lifecycle channel.
    /// Useful for topology visualization and monitoring with minimal overhead (events only on creation/deletion).
    /// </remarks>
    public bool EnableLifecycleTracking { get; set; } = false;

    /// <summary>
    /// Enable verbose logging of publish operations.
    /// </summary>
    /// <remarks>
    /// When enabled, logs every Publish() call at Trace level.
    /// Useful for debugging but adds significant logging overhead.
    /// </remarks>
    public bool EnablePublishLogging { get; set; } = false;

    /// <summary>
    /// Prefix for system channels.
    /// </summary>
    /// <remarks>
    /// System channels (like message tracing) use this prefix.
    /// Default is "$" (e.g., "$message.traces").
    /// </remarks>
    public string SystemChannelPrefix { get; set; } = "$";

    /// <summary>
    /// Buffer capacity for system channels.
    /// </summary>
    /// <remarks>
    /// System channels (like message tracing) use bounded buffers.
    /// Default is 1000 to match original hardcoded value.
    /// </remarks>
    public int SystemChannelBufferCapacity { get; set; } = 1000;

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (DefaultBufferCapacity.HasValue && DefaultBufferCapacity.Value <= 0)
            throw new ArgumentException($"{nameof(DefaultBufferCapacity)} must be greater than 0 when specified.", nameof(DefaultBufferCapacity));

        if (MaxChannels.HasValue && MaxChannels.Value <= 0)
            throw new ArgumentException($"{nameof(MaxChannels)} must be greater than 0 when specified.", nameof(MaxChannels));

        if (MaxChannelNameLength <= 0)
            throw new ArgumentException($"{nameof(MaxChannelNameLength)} must be greater than 0.", nameof(MaxChannelNameLength));

        if (DefaultConflationInterval < TimeSpan.Zero)
            throw new ArgumentException($"{nameof(DefaultConflationInterval)} cannot be negative.", nameof(DefaultConflationInterval));

        if (string.IsNullOrWhiteSpace(SystemChannelPrefix))
            throw new ArgumentException($"{nameof(SystemChannelPrefix)} cannot be null or whitespace.", nameof(SystemChannelPrefix));

        if (SystemChannelBufferCapacity <= 0)
            throw new ArgumentException($"{nameof(SystemChannelBufferCapacity)} must be greater than 0.", nameof(SystemChannelBufferCapacity));
    }
}
