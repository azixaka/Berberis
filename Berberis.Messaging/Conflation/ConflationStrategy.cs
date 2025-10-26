using Berberis.Messaging.Statistics;

namespace Berberis.Messaging.Conflation;

/// <summary>
/// Base class for conflation strategies that determine dynamic conflation intervals.
/// </summary>
public abstract class ConflationStrategy
{
    /// <summary>
    /// Gets the statistics tracker for the subscription.
    /// Available after initialization.
    /// </summary>
    protected StatsTracker Statistics { get; private set; } = null!;

    /// <summary>
    /// Initializes the strategy with the subscription's statistics tracker.
    /// Called internally by Subscription during construction.
    /// </summary>
    internal void Initialize(StatsTracker stats)
    {
        Statistics = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    /// <summary>
    /// Calculates the next conflation interval based on current metrics.
    /// Called by the flusher loop after each flush cycle.
    /// </summary>
    /// <param name="lastFlushDuration">Duration of the last flush cycle.</param>
    /// <returns>The interval to wait before the next flush.</returns>
    public abstract TimeSpan GetNextInterval(TimeSpan lastFlushDuration);
}
