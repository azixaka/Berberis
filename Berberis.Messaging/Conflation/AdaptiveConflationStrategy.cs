namespace Berberis.Messaging.Conflation;

/// <summary>
/// A conflation strategy that dynamically adjusts the interval based on system pressure signals.
/// </summary>
public sealed class AdaptiveConflationStrategy : ConflationStrategy
{
    private TimeSpan _lastInterval;

    /// <summary>
    /// Gets the minimum conflation interval (when system is idle).
    /// </summary>
    public TimeSpan MinInterval { get; init; }

    /// <summary>
    /// Gets the maximum conflation interval (when system is under pressure).
    /// </summary>
    public TimeSpan MaxInterval { get; init; }

    /// <summary>
    /// Gets the signal type to use for pressure calculation.
    /// </summary>
    public AdaptiveSignal Signal { get; init; }

    /// <summary>
    /// Gets the smoothing factor for interval changes (0.0-1.0).
    /// Higher values mean slower adaptation. Default is 0.7.
    /// </summary>
    public double SmoothingFactor { get; init; } = 0.7;

    /// <summary>
    /// Gets the queue length threshold for normalization.
    /// Queue length above this is considered maximum pressure. Default is 1000.
    /// </summary>
    public long QueueLengthThreshold { get; init; } = 1000;

    /// <summary>
    /// Gets the flush duration threshold (ms) for normalization.
    /// Flush duration above this is considered maximum pressure. Default is 100ms.
    /// </summary>
    public double FlushDurationThresholdMs { get; init; } = 100.0;

    /// <summary>
    /// Creates a new adaptive conflation strategy.
    /// </summary>
    public AdaptiveConflationStrategy()
    {
        _lastInterval = TimeSpan.Zero;
    }

    /// <summary>
    /// Calculates the next interval based on the chosen signal and current metrics.
    /// </summary>
    public override TimeSpan GetNextInterval(TimeSpan lastFlushDuration)
    {
        // Initialize on first call
        if (_lastInterval == TimeSpan.Zero)
            _lastInterval = MinInterval;

        var stats = Statistics.GetStats(reset: false);
        double pressure = CalculatePressure(stats, lastFlushDuration);

        // Linear interpolation between min and max based on pressure
        var targetInterval = Lerp(MinInterval, MaxInterval, pressure);

        // Apply smoothing to avoid oscillations
        _lastInterval = Smooth(_lastInterval, targetInterval, SmoothingFactor);

        return _lastInterval;
    }

    private double CalculatePressure(Statistics.Stats stats, TimeSpan lastFlushDuration)
    {
        double pressure = Signal switch
        {
            AdaptiveSignal.QueueLength =>
                Math.Clamp(stats.QueueLength / (double)QueueLengthThreshold, 0.0, 1.0),

            AdaptiveSignal.ConflationRatio =>
                Math.Clamp(1.0 - stats.ConflationRatio, 0.0, 1.0),

            AdaptiveSignal.FlushDuration =>
                Math.Clamp(stats.AvgFlushDurationMs / FlushDurationThresholdMs, 0.0, 1.0),

            AdaptiveSignal.Combined => CalculateCombinedPressure(stats),

            _ => 0.0
        };

        // Handle NaN or Infinity cases (e.g., division by zero in stats)
        return double.IsNaN(pressure) || double.IsInfinity(pressure) ? 0.0 : pressure;
    }

    private double CalculateCombinedPressure(Statistics.Stats stats)
    {
        var queuePressure = Math.Clamp(stats.QueueLength / (double)QueueLengthThreshold, 0.0, 1.0);
        var ratioPressure = Math.Clamp(1.0 - stats.ConflationRatio, 0.0, 1.0);
        var flushPressure = Math.Clamp(stats.AvgFlushDurationMs / FlushDurationThresholdMs, 0.0, 1.0);

        // Sanitize NaN/Infinity values
        if (double.IsNaN(ratioPressure) || double.IsInfinity(ratioPressure))
            ratioPressure = 0.0;
        if (double.IsNaN(flushPressure) || double.IsInfinity(flushPressure))
            flushPressure = 0.0;

        // Weighted average: queue is most important, then ratio, then flush duration
        return (queuePressure * 0.5) + (ratioPressure * 0.3) + (flushPressure * 0.2);
    }

    private static TimeSpan Lerp(TimeSpan min, TimeSpan max, double t)
    {
        var minMs = min.TotalMilliseconds;
        var maxMs = max.TotalMilliseconds;
        var resultMs = minMs + (t * (maxMs - minMs));
        return TimeSpan.FromMilliseconds(resultMs);
    }

    private static TimeSpan Smooth(TimeSpan current, TimeSpan target, double smoothingFactor)
    {
        var currentMs = current.TotalMilliseconds;
        var targetMs = target.TotalMilliseconds;
        var resultMs = (currentMs * smoothingFactor) + (targetMs * (1.0 - smoothingFactor));
        return TimeSpan.FromMilliseconds(resultMs);
    }
}
