using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class CompressorBlockService : BackgroundService
{
    private readonly ILogger<CompressorBlockService> _logger;
    private readonly ICrossBar _xBar;

    public CompressorBlockService(ILogger<CompressorBlockService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.compressed";

        using var subscription = _xBar.Subscribe<string>("pipeline.serialised",
            msg =>
            {
                var value = $"compressed={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, true, nameof(CompressorBlockService));

                return ValueTask.CompletedTask;
            }, fetchState: true);

        await subscription.RunReadLoopAsync();
    }
}
