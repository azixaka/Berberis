using Berberis.Messaging;
using Berberis.Recorder;

namespace Berberis.SampleApp;

public sealed class StockPriceRecorderService : BackgroundService
{
    private readonly ILogger<StockPriceRecorderService> _logger;
    private readonly ICrossBar _xBar;

    public StockPriceRecorderService(ILogger<StockPriceRecorderService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var destination = "stock.prices.>";

        var serialiser = new StockPriceSerialiser();
        using var fs = File.Open(@"c:\temp\trayport.stream", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.Read);

        using var recording = _xBar.Record(destination, fs, serialiser, false, TimeSpan.Zero, stoppingToken);
        
        await recording.MessageLoop;
    }
}
