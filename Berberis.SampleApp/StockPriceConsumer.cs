using Berberis.Messaging;
using Berberis.Messaging.AspNetCore;

namespace Berberis.SampleApp;

public class StockPriceConsumer : BerberisConsumer<StockPrice>
{
    private readonly ILogger<StockPriceConsumer> _logger;

    public StockPriceConsumer(ILogger<StockPriceConsumer> logger) : base("stock.prices")
    {
        _logger = logger;
    }

    protected override ValueTask Consume(Message<StockPrice> message, ISubscription subscription)
    {
        _logger.LogInformation("Subscription [{SubId}] got Message {MsgId}. [{Symbol}={Price:N4}]", subscription.Id,
            message.Id, message.Body.Symbol, message.Body.Price);
        return ValueTask.CompletedTask;
    }
}