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
            msg => ProcessMessage(msg), fetchState: true, TimeSpan.FromSeconds(0.5), stoppingToken);

        await subscription.MessageLoop;
    }

    private async ValueTask ProcessMessage(Message<long> message)
    {
        using (_logger.BeginScope(message.CorrelationId))
        {
            _logger.LogInformation("In process");

            using (_logger.BeginScope("new"))
            {
                await Task.Delay(15);

                _logger.LogInformation("Mid");

                await Task.Delay(30);

                _logger.LogInformation("Finish");
            }
        }
    }
}
