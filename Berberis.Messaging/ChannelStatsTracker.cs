using System.Diagnostics;

namespace Berberis.Messaging;

public sealed class ChannelStatsTracker
{
    internal static long GetTicks() => Stopwatch.GetTimestamp();
    internal static float TicksToTimeMs(long ticks) => ticks * MsRatio;
    internal static float MsRatio => (float) 1000 / Stopwatch.Frequency;

    private long _totalMessagesInc;
    private long _lastMessagesInc;

    private long _totalMessagesDec;
    private long _lastMessagesDec;

    private long _lastTicks;
    private object _syncObj = new();

    internal void IncNumOfMessages() => Interlocked.Increment(ref _totalMessagesInc);

    internal void DecNumOfMessages() => Interlocked.Increment(ref _totalMessagesDec);

    public ChannelStats GetStats(bool reset = true)
    {
        var ticks = GetTicks();

        var totalMesssagesInc = Interlocked.Read(ref _totalMessagesInc);
        var totalMesssagesDec = Interlocked.Read(ref _totalMessagesDec);

        long intervalMessagesInc;
        long intervalMessagesDec;

        float timePassed;

        lock (_syncObj)
        {
            intervalMessagesInc = totalMesssagesInc - _lastMessagesInc;
            intervalMessagesDec = totalMesssagesDec - _lastMessagesDec;

            timePassed = (ticks - _lastTicks) / Stopwatch.Frequency;

            if (reset)
            {
                _lastMessagesInc = totalMesssagesInc;
                _lastMessagesDec = totalMesssagesDec;

                _lastTicks = ticks;
            }
        }

        return new ChannelStats(timePassed * 1000,
            intervalMessagesInc / timePassed,
            intervalMessagesDec / timePassed,
            totalMesssagesInc,
            totalMesssagesDec);
    }
}
