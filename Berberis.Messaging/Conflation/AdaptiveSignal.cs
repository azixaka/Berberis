namespace Berberis.Messaging.Conflation;

/// <summary>
/// Signals that can be used to adapt the conflation interval dynamically.
/// </summary>
public enum AdaptiveSignal
{
    /// <summary>
    /// Use queue length as the pressure signal.
    /// Higher queue length increases the conflation interval.
    /// </summary>
    QueueLength,

    /// <summary>
    /// Use conflation ratio (ProcessRate/DequeueRate) as the pressure signal.
    /// Lower ratio (more conflation) increases the conflation interval.
    /// </summary>
    ConflationRatio,

    /// <summary>
    /// Use flush duration as the pressure signal.
    /// Longer flush duration increases the conflation interval.
    /// </summary>
    FlushDuration,

    /// <summary>
    /// Use a weighted combination of all signals.
    /// </summary>
    Combined
}
