using System.Diagnostics;

namespace Berberis.Messaging;

public sealed class StatsTracker
{
    internal static long GetTicks() => Stopwatch.GetTimestamp();
    internal static float TicksToTimeMs(long ticks) => ticks * MsRatio;
    internal static float MsRatio => (float) 1000 / Stopwatch.Frequency;

    private long _totalMessagesInc;
    private long _lastMessagesInc;

    private long _totalMessagesDec;
    private long _lastMessagesDec;

    private long _totalMessagesProcessed;
    private long _lastMessagesProcessed;

    private long _totalLatencyTicks;
    private long _lastLatencyTicks;

    private long _totalServiceTicks;
    private long _lastServiceTicks;

    private long _lastTicks;
    private object _syncObj = new();

    internal void IncNumOfMessages() => Interlocked.Increment(ref _totalMessagesInc);

    internal void DecNumOfMessages() => Interlocked.Increment(ref _totalMessagesDec);

    internal void IncNumOfProcessedMessages() => Interlocked.Increment(ref _totalMessagesProcessed);

    internal long RecordLatency(long startTicks)
    {
        var latency = GetTicks() - startTicks;
        Interlocked.Add(ref _totalLatencyTicks, latency);
        return latency;
    }

    internal long RecordServiceTime(long startTicks)
    {
        var svcTime = GetTicks() - startTicks;
        Interlocked.Add(ref _totalServiceTicks, svcTime);
        return svcTime;
    }

    public Stats GetStats(bool reset = true)
    {
        var ticks = GetTicks();

        var totalMesssagesInc = Interlocked.Read(ref _totalMessagesInc);
        var totalMesssagesDec = Interlocked.Read(ref _totalMessagesDec);
        var totalMesssagesProcessed = Interlocked.Read(ref _totalMessagesProcessed);

        var totalLatencyTicks = Interlocked.Read(ref _totalLatencyTicks);
        var totalServiceTicks = Interlocked.Read(ref _totalServiceTicks);

        long intervalMessagesInc;
        long intervalMessagesDec;
        long intervalMessagesProcessed;

        long intervalLatencyTicks;
        long intervalSvcTicks;

        float timePassed;

        lock (_syncObj)
        {
            intervalMessagesInc = totalMesssagesInc - _lastMessagesInc;
            intervalMessagesDec = totalMesssagesDec - _lastMessagesDec;
            intervalMessagesProcessed = totalMesssagesProcessed - _lastMessagesProcessed;

            intervalLatencyTicks = totalLatencyTicks - _lastLatencyTicks;
            intervalSvcTicks = totalServiceTicks - _lastServiceTicks;

            timePassed = (ticks - _lastTicks) / Stopwatch.Frequency;

            if (reset)
            {
                _lastMessagesInc = totalMesssagesInc;
                _lastMessagesDec = totalMesssagesDec;
                _lastMessagesProcessed = totalMesssagesProcessed;

                _lastLatencyTicks = totalLatencyTicks;
                _lastServiceTicks = totalServiceTicks;

                _lastTicks = ticks;
            }
        }

        var intervalLatencyTimeMs = intervalLatencyTicks * MsRatio;
        var intervalSvcTimeMs = intervalSvcTicks * MsRatio;

        var avgLatencyTime = intervalMessagesDec == 0 ? 0 : intervalLatencyTimeMs / intervalMessagesDec;
        var avgServiceTime = intervalMessagesDec == 0 ? 0 : intervalSvcTimeMs / intervalMessagesDec;

        return new Stats(timePassed * 1000,
            intervalMessagesInc / timePassed,
            intervalMessagesDec / timePassed,
            intervalMessagesProcessed / timePassed,
            totalMesssagesInc,
            totalMesssagesDec,
            totalMesssagesProcessed,
            avgLatencyTime,
            avgServiceTime);
    }
}
