using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class StockPriceConsumerService : BackgroundService
{
    private readonly ILogger<StockPriceConsumerService> _logger;
    private readonly ICrossBar _xBar;

    public StockPriceConsumerService(ILogger<StockPriceConsumerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000);

        var destination = "stock.prices";

        long subId = 0;
        using var subscription = _xBar.Subscribe<StockPrice>(destination,
            msg =>
            {
                _logger.LogInformation("Subscription [{subId}] got Message {msgId}. [{symbol}={price:N4}]", subId, msg.Id, msg.Body.Symbol, msg.Body);
                return ValueTask.CompletedTask;
            }, fetchState: true, conflationIntervalMilliseconds: 1000);

        subId = subscription.Id;
        await subscription.RunReadLoopAsync();
    }
}
