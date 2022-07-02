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

        using var subscription = _xBar.Subscribe<string>(destination,
            msg =>
            {
                _logger.LogInformation("Got Message [Id={msgId}, Time={time}]", msg.Id, msg.Body);
                return ValueTask.CompletedTask;
            });

        await subscription.MessageLoop;
    }
}
