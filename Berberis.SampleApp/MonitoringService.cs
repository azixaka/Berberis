using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class MonitoringService : BackgroundService
{
    private readonly ILogger<MonitoringService> _logger;
    private readonly ICrossBar _xBar;

    public MonitoringService(ILogger<MonitoringService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _xBar.TracingEnabled = true;

        using var tracingSub = _xBar.Subscribe<MessageTrace>("$message.traces",
            msg =>
            {
                _logger.LogInformation(msg.Body.ToString());

                return ValueTask.CompletedTask;
            });

        var tracingLoop = tracingSub.RunReadLoopAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var channel in _xBar.GetChannels())
            {
                _logger.LogInformation("Channel:{channel}, Type:{type}", channel.Name, channel.BodyType.Name);

                foreach (var subscription in _xBar.GetChannelSubscriptions(channel.Name))
                {
                    _logger.LogInformation("--- Subscription: {subId}, Stats: {stats}", subscription.Id, subscription.Statistics.GetStats().ToString());
                }
            }

            await Task.Delay(1000);
        }

        await tracingLoop;
    }
}
