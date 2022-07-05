using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class MaxConsumerService : BackgroundService
{
    private readonly ILogger<MaxConsumerService> _logger;
    private readonly ICrossBar _xBar;

    public MaxConsumerService(ILogger<MaxConsumerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000);

        var destination = "number.inc";

        using var subscription = _xBar.Subscribe<long>(destination,
            msg =>
            {
                //await Task.Delay(1);

                Thread.SpinWait(200000);

                return ValueTask.CompletedTask;
            }, fetchState: true, TimeSpan.FromSeconds(0.5), stoppingToken);

        await subscription.MessageLoop;
    }
}
