namespace Berberis.Messaging.Statistics;

public sealed class ExponentialWeightedMovingAverage
{
    private bool _initialised = false;

    // Smoothing/damping coefficient
    private float _alpha;

    public float AverageValue { get; private set; }

    public ExponentialWeightedMovingAverage(int samplesPerWindow)
    {
        samplesPerWindow = samplesPerWindow < 1 ? 50 : samplesPerWindow;

        // `2 / (n + 1)` is a standard ways of choosing an alpha value
        _alpha = 2f / (samplesPerWindow + 1);
    }

    public void NewSample(float value)
    {
        if (_initialised)
        {
            // Recursive weighting function: EMA[current] = EMA[previous] + alpha * (current_value - EMA[previous])
            AverageValue += _alpha * (value - AverageValue);
        }
        else
        {
            AverageValue = value;
            _initialised = true;
        }
    }

    public void Reset()
    {
        AverageValue = 0;
        _initialised = false;
    }
}