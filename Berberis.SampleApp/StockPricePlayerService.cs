using Berberis.Messaging;
using Berberis.Recorder;

namespace Berberis.SampleApp;

public sealed class StockPricePlayerService : BackgroundService
{
    private readonly ILogger<StockPricePlayerService> _logger;
    private readonly ICrossBar _xBar;

    public StockPricePlayerService(ILogger<StockPricePlayerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "stock.prices.";

        using var fs = File.OpenRead(@"c:\temp\stock.prices3.stream");

        var player = Player<StockPrice>.Create(fs, new StockPriceSerialiser());

        await foreach (var msg in player.MessagesAsync(stoppingToken))
        {
            await _xBar.Publish($"{destination}.{msg.Key}", msg);
        }
    }
}
