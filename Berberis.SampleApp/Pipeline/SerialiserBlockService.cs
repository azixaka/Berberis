using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class SerialiserBlockService : BackgroundService
{
    private readonly ILogger<SerialiserBlockService> _logger;
    private readonly ICrossBar _xBar;

    public SerialiserBlockService(ILogger<SerialiserBlockService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.serialised";

        using var subscription = _xBar.Subscribe<string>("pipeline.processed",
            msg =>
            {
                var value = $"serialised={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, true, nameof(SerialiserBlockService), msg.TagA);

                return ValueTask.CompletedTask;
            }, nameof(SerialiserBlockService), fetchState: true);

        await subscription.MessageLoop;
    }
}
