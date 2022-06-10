using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class StatefulProducerService : BackgroundService
{
    private readonly ICrossBar _xBar;

    public StatefulProducerService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000);

        while (!stoppingToken.IsCancellationRequested)
        {
            var value = DateTime.UtcNow.ToString("dd/mm/yyyy HH:mm:ss.fff");
            _xBar.Publish("stateful.time", value, key: value, store: true);
            await Task.Delay(1000);
        }
    }
}
