using System.Diagnostics;

namespace Berberis.Messaging;

public sealed class ChannelStatsTracker
{
    internal static long GetTicks() => Stopwatch.GetTimestamp();
    internal static float MsRatio => 1000f / Stopwatch.Frequency;

    private long _totalMessages;
    private long _lastMessages;

    private long _totalInterPublishTime;
    private long _lastInterPublishTime;

    private long _lastTicks;
    private readonly object _syncObj = new();

    internal void IncNumOfPublishedMessages()
    {
        var nowTicks = GetTicks();

        //todo: make atomic and benchmark against lock which might actually be cheaper than 3 interlocked ops!
        if (_lastInterPublishTime > 0)
        {
            Interlocked.Add(ref _totalInterPublishTime, (nowTicks - _lastInterPublishTime));
        }

        Interlocked.Exchange(ref _lastInterPublishTime, nowTicks);

        Interlocked.Increment(ref _totalMessages);
    }

    public ChannelStats GetStats(bool reset = true)
    {
        var ticks = GetTicks();

        var totalMessagesInc = Interlocked.Read(ref _totalMessages);

        long intervalMessagesInc;

        float timePassed;

        lock (_syncObj)
        {
            intervalMessagesInc = totalMessagesInc - _lastMessages;

            timePassed = (float)(ticks - _lastTicks) / Stopwatch.Frequency;

            if (reset)
            {
                _lastMessages = totalMessagesInc;
                _lastTicks = ticks;
            }
        }

        var totalInterProcessTimeMs = Interlocked.Read(ref _totalInterPublishTime) * MsRatio;

        return new ChannelStats(timePassed * 1000,
            intervalMessagesInc / timePassed,
            totalInterProcessTimeMs,
            totalMessagesInc);
    }
}
