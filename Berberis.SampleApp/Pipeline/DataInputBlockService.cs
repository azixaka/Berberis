using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class DataInputBlockService : BackgroundService
{
    private readonly ICrossBar _xBar;

    public DataInputBlockService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.received";

        while (!stoppingToken.IsCancellationRequested)
        {
            int key = 0;

            for (long i = 0; i < long.MaxValue; i++)
            {
                _ = _xBar.Publish(destination, i, _xBar.GetNextCorrelationId(), key.ToString(), store: true, nameof(DataInputBlockService));

                if (key++ > 100)
                    key = 0;

                await Task.Delay(1000);
            }
        }
    }
}