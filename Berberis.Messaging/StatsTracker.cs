using Berberis.Messaging.Statistics;
using System.Diagnostics;

namespace Berberis.Messaging;

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

    private long _lastTicks;
    private object _syncObj = new();

    private readonly ExponentialWeightedMovingAverage _latencyEwma = new(50);
    private readonly ExponentialWeightedMovingAverage _svcTimeEwma = new(50);

    private readonly bool _includeP90Stats;
    private readonly MovingPercentile? _latencyPercentile;
    private readonly MovingPercentile? _svcTimePercentile;

    public StatsTracker(bool includeP90Stats)
    {
        _includeP90Stats = includeP90Stats;
        if (_includeP90Stats)
        {
            _latencyPercentile = new(0.9f);
            _svcTimePercentile = new(0.9f);
        }
    }

    internal void IncNumOfEnqueuedMessages() => Interlocked.Increment(ref _totalMessagesEnqueued);

    internal void IncNumOfDequeuedMessages() => _totalMessagesDequeued++;

    internal void IncNumOfProcessedMessages() => _totalMessagesProcessed++;

    internal long RecordLatency(long startTicks)
    {
        var latency = GetTicks() - startTicks;
        _latencyEwma.NewSample(latency);

        if (_includeP90Stats)
            _latencyPercentile!.NewSample(latency, _latencyEwma.AverageValue);
        
        return latency;
    }

    internal long RecordServiceTime(long startTicks)
    {
        var svcTime = GetTicks() - startTicks;
        _svcTimeEwma.NewSample(svcTime);

        if (_includeP90Stats)
            _svcTimePercentile!.NewSample(svcTime, _svcTimeEwma.AverageValue);

        return svcTime;
    }

    public Stats GetStats(bool reset = true)
    {
        var ticks = GetTicks();

        var totalMesssagesEnqueued = Interlocked.Read(ref _totalMessagesEnqueued);
        var totalMesssagesDequeued = Interlocked.Read(ref _totalMessagesDequeued);
        var totalMesssagesProcessed = Interlocked.Read(ref _totalMessagesProcessed);

        long intervalMessagesDequeued;
        long intervalMessagesProcessed;

        float timePassed;

        lock (_syncObj)
        {
            intervalMessagesDequeued = totalMesssagesDequeued - _lastMessagesDequeued;
            intervalMessagesProcessed = totalMesssagesProcessed - _lastMessagesProcessed;

            timePassed = (float)(ticks - _lastTicks) / Stopwatch.Frequency;

            if (reset)
            {
                _lastMessagesDequeued = totalMesssagesDequeued;
                _lastMessagesProcessed = totalMesssagesProcessed;

                _lastTicks = ticks;

                _latencyEwma.Reset();
                _svcTimeEwma.Reset();

                if (_includeP90Stats)
                {
                    _latencyPercentile!.Reset();
                    _svcTimePercentile!.Reset();
                }
            }
        }

        return new Stats(timePassed * 1000,
            intervalMessagesDequeued / timePassed,
            intervalMessagesProcessed / timePassed,
            totalMesssagesEnqueued,
            totalMesssagesDequeued,
            totalMesssagesProcessed,
            _latencyEwma.AverageValue * MsRatio,
            _svcTimeEwma.AverageValue * MsRatio,
            _includeP90Stats ? _latencyPercentile!.PercentileValue * MsRatio : float.NaN,
            _includeP90Stats ? _svcTimePercentile!.PercentileValue * MsRatio : float.NaN);
    }
}
