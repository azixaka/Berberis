using System.Diagnostics;

namespace Berberis.Messaging.Statistics;

/// <summary>Tracks subscription performance statistics.</summary>
public sealed class StatsTracker
{
    internal static long GetTicks() => Stopwatch.GetTimestamp();
    internal static float TicksToTimeMs(long ticks) => ticks * MsRatio;
    internal static float MsRatio => 1000f / Stopwatch.Frequency;

    private long _totalMessagesEnqueued;

    private long _totalMessagesDequeued;
    private long _lastMessagesDequeued;

    private long _totalMessagesProcessed;
    private long _lastMessagesProcessed;

    private long _numOfTimeouts;

    private long _lastTicks;
    private object _syncObj = new();

    private readonly ExponentialWeightedMovingAverage _latencyEwma;
    private readonly ExponentialWeightedMovingAverage _svcTimeEwma;

    private readonly bool _includePercentileStats;
    private readonly MovingPercentile? _latencyPercentile;
    private readonly MovingPercentile? _svcTimePercentile;

    /// <summary>Creates statistics tracker with options.</summary>
    public StatsTracker(StatsOptions statsOptions)
    {
        StatsOptions = statsOptions;

        _latencyEwma = new(statsOptions.EwmaWindowSize);
        _svcTimeEwma = new(statsOptions.EwmaWindowSize);

        _includePercentileStats = statsOptions.PercentileEnabled;

        if (_includePercentileStats)
        {
            _latencyPercentile = new(statsOptions.Percentile!.Value, statsOptions.Alpha, statsOptions.Delta);
            _svcTimePercentile = new(statsOptions.Percentile!.Value, statsOptions.Alpha, statsOptions.Delta);
        }

        _lastTicks = GetTicks();
    }

    /// <summary>Statistics collection options.</summary>
    public StatsOptions StatsOptions { get; }

    internal void IncNumOfEnqueuedMessages() => Interlocked.Increment(ref _totalMessagesEnqueued);

    internal void IncNumOfDequeuedMessages() => _totalMessagesDequeued++;

    internal void IncNumOfProcessedMessages() => _totalMessagesProcessed++;

    internal void IncNumOfTimeouts() => Interlocked.Increment(ref _numOfTimeouts);

    internal long GetNumOfTimeouts() => Volatile.Read(ref _numOfTimeouts);

    internal long RecordLatency(long startTicks)
    {
        var latency = GetTicks() - startTicks;
        _latencyEwma.NewSample(latency);

        if (_includePercentileStats)
            _latencyPercentile!.NewSample(latency, _latencyEwma.AverageValue);

        return latency;
    }

    internal long RecordServiceTime(long startTicks)
    {
        var svcTime = GetTicks() - startTicks;
        _svcTimeEwma.NewSample(svcTime);

        if (_includePercentileStats)
            _svcTimePercentile!.NewSample(svcTime, _svcTimeEwma.AverageValue);

        return svcTime;
    }

    /// <summary>Gets statistics snapshot.</summary>
    /// <param name="reset">Reset counters after snapshot.</param>
    public Stats GetStats(bool reset)
    {
        var ticks = GetTicks();

        var totalMesssagesEnqueued = Interlocked.Read(ref _totalMessagesEnqueued);
        var totalMesssagesDequeued = Interlocked.Read(ref _totalMessagesDequeued);
        var totalMesssagesProcessed = Interlocked.Read(ref _totalMessagesProcessed);

        long intervalMessagesDequeued;
        long intervalMessagesProcessed;

        float timePassed;

        float latAvg;
        float latMin;
        float latMax;
        float svcAvg;
        float svcMin;
        float svcMax;

        float latPct = float.NaN;
        float svcPct = float.NaN;

        lock (_syncObj)
        {
            intervalMessagesDequeued = totalMesssagesDequeued - _lastMessagesDequeued;
            intervalMessagesProcessed = totalMesssagesProcessed - _lastMessagesProcessed;

            timePassed = (float)(ticks - _lastTicks) / Stopwatch.Frequency;

            latAvg = _latencyEwma.AverageValue;
            latMin = _latencyEwma.MinValue;
            latMax = _latencyEwma.MaxValue;

            svcAvg = _svcTimeEwma.AverageValue;
            svcMin = _svcTimeEwma.MinValue;
            svcMax = _svcTimeEwma.MaxValue;

            latPct = float.NaN;
            svcPct = float.NaN;

            if (_includePercentileStats)
            {
                latPct = _latencyPercentile!.PercentileValue;
                svcPct = _svcTimePercentile!.PercentileValue;
            }

            if (reset)
            {
                _lastMessagesDequeued = totalMesssagesDequeued;
                _lastMessagesProcessed = totalMesssagesProcessed;

                _lastTicks = ticks;

                _latencyEwma.Reset();
                _svcTimeEwma.Reset();

                if (_includePercentileStats)
                {
                    _latencyPercentile!.Reset();
                    _svcTimePercentile!.Reset();
                }
            }
        }

        var numOfTimeouts = GetNumOfTimeouts();

        return new Stats(timePassed * 1000,
            intervalMessagesDequeued / timePassed,
            intervalMessagesProcessed / timePassed,
            totalMesssagesEnqueued,
            totalMesssagesDequeued,
            totalMesssagesProcessed,
            latAvg * MsRatio,
            svcAvg * MsRatio,
            latPct * MsRatio,
            svcPct * MsRatio,
            latMin * MsRatio,
            latMax * MsRatio,
            svcMin * MsRatio,
            svcMax * MsRatio,
            numOfTimeouts
            );
    }
}
