namespace Berberis.Messaging.Statistics;

public sealed class MovingPercentile
{
    private bool _initialised;
    private readonly float _percentile;

    private readonly float _alpha;
    private float _delta;

    public float PercentileValue { get; private set; }

    public MovingPercentile(float percentile, float alpha = 0.05f, float delta = 0.05f)
    {
        _percentile = percentile;
        _alpha = alpha;
        _delta = delta;
    }

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

    public void NewSample(float value, float ewma)
    {
        var sigma = (float) Math.Sqrt(Math.Abs(ewma - value));
        _delta = sigma * _alpha;
        NewSample(value);
    }

    public void Reset()
    {
        _initialised = false;
    }
}
