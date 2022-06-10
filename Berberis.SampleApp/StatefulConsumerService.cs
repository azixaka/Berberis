using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class StatefulConsumerService : BackgroundService
{
    private readonly ILogger<StatefulConsumerService> _logger;
    private readonly ICrossBar _xBar;

    public StatefulConsumerService(ILogger<StatefulConsumerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000);

        long subId = 0;
        using var subscription = _xBar.Subscribe<string>("stateful.time",
            msg =>
            {
                _logger.LogInformation("Subscription [{subId}] got Message [Id={msgId}, Time={time}]", subId, msg.Id, msg.Body);
                return ValueTask.CompletedTask;
            }, fetchState: true);

        subId = subscription.Id;
        await subscription.RunReadLoopAsync();
    }
}
