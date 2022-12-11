using Berberis.Messaging;
using Berberis.StatsReporters;
using System.Diagnostics;

namespace Berberis.SampleApp;

public sealed class DataInputBlockService : BackgroundService
{
    private readonly ICrossBar _xBar;
    private readonly int _minTickInterval;
    private readonly int _maxTickInterval;

    public DataInputBlockService(ICrossBar xBar)
    {
        _xBar = xBar;
        _minTickInterval = 0;
        _maxTickInterval = 5;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.received";
        var random = new Random();

        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            int key = 0;

            for (long i = 0; i < long.MaxValue; i++)
            {
                _ = _xBar.Publish(destination, i, _xBar.GetNextCorrelationId(), key.ToString(), store: true, nameof(DataInputBlockService), ServiceTimeTracker.GetTicks());

                if (key++ > 10)
                    key = 0;

                var waitMs = random.Next(_minTickInterval, _maxTickInterval);

                var sw = Stopwatch.StartNew();
                var spin = new SpinWait();

                while (sw.ElapsedMilliseconds < waitMs)
                {
                    spin.SpinOnce();
                }
            }
        }
    }
}