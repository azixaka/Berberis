using Berberis.Messaging;
using Berberis.Messaging.Statistics;
using System.Diagnostics;

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
        var destination = "stock.prices.>";

        using var subscription = _xBar.Subscribe<StockPrice>(destination,
            msg =>
            {
                //_logger.LogInformation("Got Message {msgId}. [{symbol}={price:N4}]", msg.Id, msg.Body.Symbol, msg.Body.Price);

                //var passed = ((float)(Stopwatch.GetTimestamp() - msg.TagA) / Stopwatch.Frequency) * 1000;
                //Debug.WriteLine(passed);
                return ValueTask.CompletedTask;
            }, nameof(StockPriceWildcardConsumerService), 
            fetchState: true,
            TimeSpan.Zero, 
            new StatsOptions(percentile: 0.9f), stoppingToken);

        //await Task.Delay(2000);

        //subscription.IsDetached = true;
        //await Task.Delay(2000);
        //subscription.IsDetached = false;

        //await Task.Delay(2000);

        //subscription.IsProcessingSuspended = true;
        //await Task.Delay(2000);
        //subscription.IsProcessingSuspended = false;

        await subscription.MessageLoop;
    }
}
