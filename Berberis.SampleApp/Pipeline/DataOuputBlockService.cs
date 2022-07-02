using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class DataOutputBlockService : BackgroundService
{
    private readonly ICrossBar _xBar;

    public DataOutputBlockService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.sent";

        using var subscription = _xBar.Subscribe<string>("pipeline.compressed",
            msg =>
            {
                var value = $"sent={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, false, nameof(DataOutputBlockService));

                return ValueTask.CompletedTask;
            }, nameof(DataOutputBlockService), fetchState: true);

        await subscription.MessageLoop;
    }
}