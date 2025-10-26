namespace Berberis.Messaging.Conflation;

/// <summary>
/// A conflation strategy that always returns a fixed interval.
/// This is the default strategy used for backward compatibility.
/// </summary>
public sealed class FixedConflationStrategy : ConflationStrategy
{
    private readonly TimeSpan _interval;

    /// <summary>
    /// Creates a new fixed conflation strategy.
    /// </summary>
    /// <param name="interval">The fixed interval to use for conflation.</param>
    public FixedConflationStrategy(TimeSpan interval)
    {
        _interval = interval;
    }

    /// <summary>
    /// Returns the fixed interval, ignoring all metrics.
    /// </summary>
    public override TimeSpan GetNextInterval(TimeSpan lastFlushDuration)
    {
        return _interval;
    }
}
