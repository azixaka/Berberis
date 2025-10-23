using Berberis.Messaging.Exceptions;

namespace Berberis.Messaging;

/// <summary>
/// Strategy for handling slow consumers when the subscription buffer reaches capacity.
/// </summary>
/// <remarks>
/// <para>
/// When a subscriber cannot keep up with the message rate, the buffer fills up.
/// This enum controls what happens when a new message arrives and the buffer is full.
/// </para>
/// <para>
/// PERFORMANCE NOTE: All strategies are allocation-free. The choice of strategy
/// does not affect memory allocation on the hot path.
/// </para>
/// </remarks>
public enum SlowConsumerStrategy : byte
{
    /// <summary>
    /// Skip the incoming message if the buffer is full.
    /// The subscription continues running, but some messages may be lost.
    /// This is the default and recommended strategy for high-frequency updates
    /// where the latest value is more important than historical values.
    /// </summary>
    SkipUpdates,

    /// <summary>
    /// Fail and dispose the subscription when the buffer is full.
    /// Throws <see cref="FailedSubscriptionException"/> and stops message delivery.
    /// Use this when message loss is unacceptable and the consumer must be alerted
    /// that it cannot keep up with the message rate.
    /// </summary>
    FailSubscription,

    /// <summary>
    /// Conflate messages with the same key and skip updates without keys.
    /// When the buffer is full, messages with keys are conflated (only the latest
    /// message per key is kept), while messages without keys are skipped.
    /// This strategy provides a balance between preserving important state updates
    /// and preventing subscription failure.
    /// </summary>
    /// <remarks>
    /// Note: Messages without keys cannot be conflated, so they will be skipped.
    /// Use this strategy when working with keyed messages where the latest value
    /// per key is most important.
    /// </remarks>
    ConflateAndSkipUpdates
}