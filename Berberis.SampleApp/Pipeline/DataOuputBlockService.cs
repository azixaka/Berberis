using Berberis.Messaging;
using Berberis.StatsReporters;

namespace Berberis.SampleApp;

public sealed class DataOutputBlockService : BackgroundService
{
    private readonly ICrossBar _xBar;
    private ServiceTimeTracker _globalTracker;

    public DataOutputBlockService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.sent";

        var trackerOptions = new[]
                                {
                                    new PercentileOptions(0.75f),
                                    new PercentileOptions(0.9f),
                                    new PercentileOptions(0.99f)
                                };

        _globalTracker = new ServiceTimeTracker(percentileOptions: trackerOptions);

        var keyTrackers = new Dictionary<string, ServiceTimeTracker>();

        using var subscription = _xBar.Subscribe<string>("pipeline.compressed",
            msg =>
            {
                var value = $"sent={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, false, nameof(DataOutputBlockService), msg.TagA);

                ServiceTimeTracker tracker;

                lock (keyTrackers)
                {
                    if (keyTrackers.TryGetValue(msg.Key, out tracker))
                    {

                    }
                    else
                    {
                        tracker = new ServiceTimeTracker(percentileOptions: trackerOptions);
                        keyTrackers[msg.Key] = tracker;
                    }
                }

                tracker.RecordServiceTime(msg.TagA);
                _globalTracker.RecordServiceTime(msg.TagA);

                return ValueTask.CompletedTask;
            }, nameof(DataOutputBlockService), fetchState: true);

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        var nsr = new NetworkStatsReporter();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var s = nsr.GetStats();


            var stats = _globalTracker.GetStats(true);
            Console.WriteLine($"[*] Avg:{stats.AvgServiceTimeMs:F2} | Min:{stats.MinServiceTimeMs:F2} | Max:{stats.MaxServiceTimeMs:F2} | Rate:{stats.ProcessRate:F0} | {string.Join(';', stats.PercentileValues.Select(t => $"{t.percentile * 100}% = {t.value:F2}"))}");

            lock (keyTrackers)
            {
                foreach (var (key, tracker) in keyTrackers)
                {
                    stats = tracker.GetStats(true);
                    Console.WriteLine($"[{key}] Avg:{stats.AvgServiceTimeMs:F2} | Min:{stats.MinServiceTimeMs:F2} | Max:{stats.MaxServiceTimeMs:F2} | Rate:{stats.ProcessRate:F0} | {string.Join(';', stats.PercentileValues.Select(t => $"{t.percentile * 100}% = {t.value:F2}"))}");
                }
            }
        }

        await subscription.MessageLoop;
    }
}