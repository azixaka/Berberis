using Berberis.Messaging;
using System.Runtime.InteropServices;

namespace Berberis.SampleApp.Examples.StockPrice;

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
        //await Task.Delay(5000);

        var destination = "stock.prices";

        using var subscription = _xBar.Subscribe<StockPrice>(destination,
            msg =>
            {
                //_logger.LogInformation("Got Message {msgId} {type}. [{symbol}={price:N4}]", msg.Id, msg.MessageType.ToString(), msg.Body.Symbol, msg.Body.Price);
                return ValueTask.CompletedTask;
            }, fetchState: true);

        //await Task.Delay(5000);

        //var snapshot = _xBar.GetChannelState<StockPrice>(destination).ToList();

        //_xBar.ResetChannel<StockPrice>(destination);

        //var snapshot2 = _xBar.GetChannelState<StockPrice>(destination).ToList();

        //if (_xBar.TryDeleteMessage<StockPrice>(destination, "amzn", out var deletedMsg)) 
        //{
        //}

        await subscription.MessageLoop;
    }
}
