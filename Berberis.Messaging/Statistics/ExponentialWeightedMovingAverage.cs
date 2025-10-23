namespace Berberis.Messaging.Statistics;

/// <summary>Exponential weighted moving average calculator.</summary>
public sealed class ExponentialWeightedMovingAverage
{
    private bool _initialised = false;

    // Smoothing/damping coefficient
    private float _alpha;

    /// <summary>Gets the exponentially weighted moving average value.</summary>
    public float AverageValue { get; private set; }

    /// <summary>Gets the minimum value observed.</summary>
    public float MinValue { get; private set; }

    /// <summary>Gets the maximum value observed.</summary>
    public float MaxValue { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialWeightedMovingAverage"/> class.
    /// </summary>
    /// <param name="samplesPerWindow">Number of samples per window for smoothing (minimum 1).</param>
    public ExponentialWeightedMovingAverage(int samplesPerWindow)
    {
        samplesPerWindow = samplesPerWindow < 1 ? 50 : samplesPerWindow;

        // `2 / (n + 1)` is a standard ways of choosing an alpha value
        _alpha = 2f / (samplesPerWindow + 1);
    }

    /// <summary>
    /// Adds a new sample to the moving average calculation.
    /// </summary>
    /// <param name="value">The sample value to add.</param>
    public void NewSample(float value)
    {
        if (_initialised)
        {
            // Recursive weighting function: EMA[current] = EMA[previous] + alpha * (current_value - EMA[previous])
            AverageValue += _alpha * (value - AverageValue);

            MinValue = Math.Min(MinValue, value);
            MaxValue = Math.Max(MaxValue, value);
        }
        else
        {
            AverageValue = value;
            MinValue = value;
            MaxValue = value;
            _initialised = true;
        }
    }

    /// <summary>
    /// Resets all statistics to zero.
    /// </summary>
    public void Reset()
    {
        AverageValue = 0;
        MinValue = 0;
        MaxValue = 0;
        _initialised = false;
    }
}