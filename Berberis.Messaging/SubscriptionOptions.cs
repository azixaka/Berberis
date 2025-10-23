using Berberis.Messaging.Exceptions;

namespace Berberis.Messaging;

/// <summary>
/// Options for configuring subscription behavior.
/// </summary>
public class SubscriptionOptions
{
    /// <summary>
    /// Maximum time allowed for message handler execution.
    /// If null, handlers can run indefinitely (not recommended for production).
    /// Default: null (no timeout).
    /// </summary>
    /// <remarks>
    /// When a handler times out, the subscription continues processing subsequent messages.
    /// The timeout applies per-message, not to the entire subscription.
    ///
    /// PERFORMANCE NOTE: Enabling timeouts introduces minimal allocation overhead (Task conversion for async handlers).
    /// For synchronously-completing handlers (ValueTask.IsCompleted == true), there is zero allocation overhead.
    /// </remarks>
    public TimeSpan? HandlerTimeout { get; set; }

    /// <summary>
    /// Callback invoked when a handler times out.
    /// Useful for alerting, logging, or taking corrective action.
    /// </summary>
    /// <remarks>
    /// This callback is invoked synchronously before continuing to the next message.
    /// Keep the callback fast to avoid blocking message processing.
    /// </remarks>
    public Action<HandlerTimeoutException>? OnTimeout { get; set; }
}
