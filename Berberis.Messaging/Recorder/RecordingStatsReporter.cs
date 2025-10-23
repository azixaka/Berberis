using System.Diagnostics;

namespace Berberis.Messaging.Recorder;

/// <summary>
/// Tracks and reports statistics for recording operations.
/// Thread-safe for concurrent Start/Stop calls.
/// </summary>
public sealed class RecorderStatsReporter
{
    private long _totalMessages;
    private long _lastMessages;

    private long _totalServiceTicks;
    private long _lastServiceTicks;

    private long _totalBytes;
    private long _lastBytes;

    private long _lastTicks;

    private readonly object _syncObj = new();

    /// <summary>
    /// Starts timing a recording operation.
    /// </summary>
    /// <returns>The timestamp when the operation started.</returns>
    public long Start() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Stops timing a recording operation and records its metrics.
    /// </summary>
    /// <param name="startTicks">The timestamp from <see cref="Start"/>.</param>
    /// <param name="bytes">The number of bytes written in this operation.</param>
    public void Stop(long startTicks, long bytes)
    {
        Interlocked.Increment(ref _totalMessages);
        Interlocked.Add(ref _totalServiceTicks, Stopwatch.GetTimestamp() - startTicks);
        Interlocked.Add(ref _totalBytes, bytes);
    }

    /// <summary>
    /// Gets recording statistics for the interval since the last call to this method.
    /// </summary>
    /// <returns>Recording statistics including message rate, throughput, and service times.</returns>
    public RecorderStats GetStats()
    {
        var totalMesssages = Interlocked.Read(ref _totalMessages);
        var totalServiceTicks = Interlocked.Read(ref _totalServiceTicks);
        var totalBytes = Interlocked.Read(ref _totalBytes);

        long intervalMessages;
        long intervalSvcTicks;
        long intervalBytes;
        float timePassed;

        lock (_syncObj)
        {
            intervalMessages = totalMesssages - _lastMessages;
            intervalSvcTicks = totalServiceTicks - _lastServiceTicks;
            intervalBytes = totalBytes - _lastBytes;

            _lastMessages = totalMesssages;
            _lastServiceTicks = totalServiceTicks;
            _lastBytes = totalBytes;

            var ticks = Stopwatch.GetTimestamp();
            timePassed = (float)(ticks - _lastTicks) / Stopwatch.Frequency;
            _lastTicks = ticks;
        }

        var intervalSvcTimeMs = intervalSvcTicks / (float)Stopwatch.Frequency * 1000;
        var avgServiceTime = intervalMessages == 0 ? 0 : intervalSvcTimeMs / intervalMessages;

        return new RecorderStats(timePassed * 1000,
            intervalMessages / timePassed,
            totalMesssages,
            intervalBytes / timePassed,
            totalBytes,
            avgServiceTime);
    }
}
