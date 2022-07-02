using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class ProcessorBlockService : BackgroundService
{
    private readonly ILogger<ProcessorBlockService> _logger;
    private readonly ICrossBar _xBar;

    public ProcessorBlockService(ILogger<ProcessorBlockService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "pipeline.processed";

        using var subscription = _xBar.Subscribe<string>("pipeline.deserialised",
            msg =>
            {
                var value = $"processed={msg.Body}";
                _ = _xBar.Publish(destination, value, msg.CorrelationId, msg.Key, true, nameof(ProcessorBlockService));

                return ValueTask.CompletedTask;
            }, nameof(ProcessorBlockService), fetchState: true);

        await subscription.MessageLoop;
    }
}
