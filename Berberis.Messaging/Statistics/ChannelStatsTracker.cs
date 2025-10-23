using System.Diagnostics;

namespace Berberis.Messaging.Statistics;

/// <summary>Tracks channel performance statistics.</summary>
public sealed class ChannelStatsTracker
{
    internal static long GetTicks() => Stopwatch.GetTimestamp();
    internal static float MsRatio => 1000f / Stopwatch.Frequency;

    private long _totalMessages;
    private long _lastMessages;

    private long _lastTicks;
    private object _syncObj = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelStatsTracker"/> class.
    /// </summary>
    public ChannelStatsTracker()
    {
        _lastTicks = GetTicks();
    }

    internal void IncNumOfPublishedMessages() => Interlocked.Increment(ref _totalMessages);

    /// <summary>
    /// Gets channel statistics for the interval since the last call (if reset is true).
    /// </summary>
    /// <param name="reset">If true, resets the interval counters after reading.</param>
    /// <returns>Channel statistics including message rate and total messages.</returns>
    public ChannelStats GetStats(bool reset)
    {
        var ticks = GetTicks();

        var totalMesssagesInc = Interlocked.Read(ref _totalMessages);

        long intervalMessagesInc;

        float timePassed;

        lock (_syncObj)
        {
            intervalMessagesInc = totalMesssagesInc - _lastMessages;

            timePassed = (float)(ticks - _lastTicks) / Stopwatch.Frequency;

            if (reset)
            {
                _lastMessages = totalMesssagesInc;
                _lastTicks = ticks;
            }
        }

        return new ChannelStats(timePassed * 1000,
            intervalMessagesInc / timePassed,
            totalMesssagesInc);
    }
}
