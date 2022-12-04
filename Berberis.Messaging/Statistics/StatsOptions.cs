namespace Berberis.Messaging.Statistics;

public readonly struct StatsOptions
{
    public readonly float Percentile;
    public readonly float Alpha;
    public readonly float Delta;
    public readonly int EwmaWindowSize;

    public StatsOptions(float percentile = float.NaN, float alpha = 0.05f, float delta = 0.05f, int ewmaWindowSize = 50)
    {
        Percentile = percentile;
        Alpha = alpha;
        Delta = delta;
        EwmaWindowSize = ewmaWindowSize;
    }

    public bool PercentileEnabled => Percentile != float.NaN && Percentile > 0.01 && Percentile < 0.99;
}