using Berberis.Messaging;

namespace Berberis.SampleApp.Examples.TimeMessages;

public sealed class TimeProducerService : BackgroundService
{
    private readonly ICrossBar _xBar;

    public TimeProducerService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000);

        var destination = "current.time";

        while (!stoppingToken.IsCancellationRequested)
        {
            _xBar.Publish(destination, DateTime.UtcNow.ToString("dd/mm/yyyy HH:mm:ss.fff"));
            await Task.Delay(1000);
        }
    }
}
