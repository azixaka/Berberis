using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class StockPriceWildcardConsumerService : BackgroundService
{
    private readonly ILogger<StockPriceWildcardConsumerService> _logger;
    private readonly ICrossBar _xBar;

    public StockPriceWildcardConsumerService(ILogger<StockPriceWildcardConsumerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken);

        const string destination = "stock.prices.>";

        using var subscription = _xBar.Subscribe<StockPrice>(destination,
            msg =>
            {
                _logger.LogInformation("Got Message {msgId}. [{symbol}={price:N4}]", msg.Id, msg.Body.Symbol, msg.Body.Price);
                return ValueTask.CompletedTask;
            }, fetchState: true, TimeSpan.FromSeconds(1));

        await subscription.MessageLoop;
    }
}
