using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class ProcessesConsumerService2 : BackgroundService
{
    private readonly ILogger<ProcessesConsumerService2> _logger;
    private readonly ICrossBar _xBar;

    public ProcessesConsumerService2(ILogger<ProcessesConsumerService2> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000);

        var destination = "processes.info";

        using var subscription = _xBar.Subscribe<ProcessInfo>(destination,
            msg =>
            {
                _logger.LogDebug("Got Message [Id={msgId}, Ms={cpuTime}]", msg.Id, msg.Body.CpuTimeMs);
                return ValueTask.CompletedTask;
            });

        await subscription.MessageLoop;
    }
}
