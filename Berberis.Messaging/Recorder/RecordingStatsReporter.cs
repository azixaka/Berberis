using System.Diagnostics;

namespace Berberis.Messaging.Recorder;

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

    public long Start() => Stopwatch.GetTimestamp();

    public void Stop(long startTicks, long bytes)
    {
        Interlocked.Increment(ref _totalMessages);
        Interlocked.Add(ref _totalServiceTicks, Stopwatch.GetTimestamp() - startTicks);
        Interlocked.Add(ref _totalBytes, bytes);
    }

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
            intervalBytes,
            totalBytes,
            avgServiceTime);
    }
}
