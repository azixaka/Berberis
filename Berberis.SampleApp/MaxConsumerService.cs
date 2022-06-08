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
                return ValueTask.CompletedTask;
            });

        await Task.WhenAll(subscription.RunReadLoopAsync());
    }
}
