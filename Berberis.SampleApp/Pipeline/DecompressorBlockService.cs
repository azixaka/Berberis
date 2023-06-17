using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class DecompressorBlockService : BackgroundService
{
    private readonly ILogger<DecompressorBlockService> _logger;
    private readonly ICrossBar _xBar;

    public DecompressorBlockService(ILogger<DecompressorBlockService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.decompressed";

        int k = 0;

        using var subscription = _xBar.Subscribe<long>("pipeline.received",
            msg =>
            {
                var value = $"decompressed={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, true, nameof(DecompressorBlockService), msg.TagA);

                if (k++ == 1000000)
                    throw new Exception("Aha");

                return ValueTask.CompletedTask;
            }, nameof(DecompressorBlockService), fetchState: true);

        await subscription.MessageLoop;
    }
}
