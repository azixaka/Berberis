using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class ChannelsMonitoringService : BackgroundService
{
    private readonly ILogger<ChannelsMonitoringService> _logger;
    private readonly ICrossBar _xBar;

    public ChannelsMonitoringService(ILogger<ChannelsMonitoringService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var channel in _xBar.GetChannels())
            {
                _logger.LogInformation("Channel:{channel}, Type:{type}", channel.Name, channel.BodyType.Name);
            }

            await Task.Delay(5000);
        }
    }
}
