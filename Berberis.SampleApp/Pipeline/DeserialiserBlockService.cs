using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class DeserialiserBlockService : BackgroundService
{
    private readonly ILogger<DeserialiserBlockService> _logger;
    private readonly ICrossBar _xBar;

    public DeserialiserBlockService(ILogger<DeserialiserBlockService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.deserialised";

        using var subscription = _xBar.Subscribe<string>("pipeline.decompressed",
            msg =>
            {
                var value = $"deserialised={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, true, nameof(DeserialiserBlockService));

                return ValueTask.CompletedTask;
            }, fetchState: true);

        await subscription.RunReadLoopAsync();
    }
}
