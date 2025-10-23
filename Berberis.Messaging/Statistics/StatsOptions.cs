namespace Berberis.Messaging.Statistics;

/// <summary>Statistics collection configuration.</summary>
public readonly struct StatsOptions
{
    /// <summary>Percentile to track (0.01-0.99).</summary>
    public readonly float? Percentile;
    /// <summary>Moving percentile alpha parameter.</summary>
    public readonly float Alpha;
    /// <summary>Moving percentile delta parameter.</summary>
    public readonly float Delta;
    /// <summary>EWMA window size.</summary>
    public readonly int EwmaWindowSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsOptions"/> struct.
    /// </summary>
    public StatsOptions(float? percentile = null, float alpha = 0.05f, float delta = 0.05f, int ewmaWindowSize = 50)
    {
        Percentile = percentile;
        Alpha = alpha;
        Delta = delta;
        EwmaWindowSize = ewmaWindowSize;
    }

    /// <summary>True if percentile tracking enabled.</summary>
    public bool PercentileEnabled => Percentile.HasValue && Percentile != float.NaN && Percentile > 0.01 && Percentile < 0.99;
}