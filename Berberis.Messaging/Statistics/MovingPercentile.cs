namespace Berberis.Messaging.Statistics;

/// <summary>Moving percentile estimator.</summary>
public sealed class MovingPercentile
{
    private bool _initialised;
    private readonly float _percentile;

    private readonly float _alpha;
    private float _delta;
    private readonly float _deltaInit;

    /// <summary>Gets the current percentile estimate.</summary>
    public float PercentileValue { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MovingPercentile"/> class.
    /// </summary>
    /// <param name="percentile">The percentile to track (0.0 to 1.0).</param>
    /// <param name="alpha">Smoothing factor (default 0.05).</param>
    /// <param name="delta">Initial step size (default 0.05).</param>
    public MovingPercentile(float percentile, float alpha = 0.05f, float delta = 0.05f)
    {
        _percentile = percentile;
        _alpha = alpha;
        _delta = _deltaInit = delta;
    }

    /// <summary>
    /// Adds a new sample to the percentile calculation.
    /// </summary>
    /// <param name="value">The sample value to add.</param>
    public void NewSample(float value)
    {
        if (_initialised)
        {
            if (value < PercentileValue)
            {
                PercentileValue -= _delta / _percentile;
            }
            else if (value > PercentileValue)
            {
                PercentileValue += _delta / (1 - _percentile);
            }
        }
        else
        {
            PercentileValue = value;
            _initialised = true;
        }
    }

    /// <summary>
    /// Adds a new sample with adaptive step size based on variance from exponential weighted moving average.
    /// </summary>
    /// <param name="value">The sample value to add.</param>
    /// <param name="ewma">The exponential weighted moving average for adaptive step sizing.</param>
    public void NewSample(float value, float ewma)
    {
        var sigma = (float) Math.Sqrt(Math.Abs(ewma - value));
        _delta = sigma * _alpha;
        NewSample(value);
    }

    /// <summary>
    /// Resets the percentile estimate to zero.
    /// </summary>
    public void Reset()
    {
        PercentileValue = 0;
        _delta = _deltaInit;
        _initialised = false;
    }
}
