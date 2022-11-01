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

        var serialiser = new StockPriceSerialiser();

        using var fs = File.Open(@"c:\temp\trayport.stream", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var player = Player<StockPrice>.Create(fs, serialiser, PlayMode.AsFastAsPossible);

        await foreach (var msg in player.MessagesAsync(stoppingToken))
        {
            await _xBar.Publish($"{destination}.{msg.Key}", msg);
        }
    }
}
