using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class TimeConsumerService : BackgroundService
{
    private readonly ILogger<TimeConsumerService> _logger;
    private readonly ICrossBar _xBar;

    public TimeConsumerService(ILogger<TimeConsumerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000);

        var destination = "current.time";

        long subId = 0;
        using var subscription = _xBar.Subscribe<string>(destination,
            msg =>
            {
                _logger.LogInformation("Subscription [{subId}] got Message [Id={msgId}, Time={time}]", subId, msg.Id, msg.Body);
                return ValueTask.CompletedTask;
            });

        subId = subscription.Id;
        await subscription.RunReadLoopAsync();
    }
}
