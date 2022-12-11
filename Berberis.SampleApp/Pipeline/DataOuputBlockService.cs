using Berberis.Messaging;
using Berberis.StatsReporters;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Berberis.SampleApp;

public sealed class DataOutputBlockService : BackgroundService
{
    private readonly ICrossBar _xBar;

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

                return ValueTask.CompletedTask;
            }, nameof(DataOutputBlockService), fetchState: true);

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            lock (keyTrackers)
            {
                foreach (var (key, tracker) in keyTrackers)
                {
                    var stats = tracker.GetStats(true);
                    Console.WriteLine($"[{key}] Avg:{stats.AvgServiceTimeMs:N2} | Min:{stats.MinServiceTimeMs:N2} | Max:{stats.MaxServiceTimeMs:N2} | Rate:{stats.ProcessRate:N0} | {string.Join(';', stats.PercentileValues.Select(t => $"{t.percentile * 100}% = {t.value:N2}"))}");
                }
            }
        }

        await subscription.MessageLoop;
    }
}