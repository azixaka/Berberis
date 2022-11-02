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

        using var fs = File.OpenWrite(@"c:\temp\stock.prices.stream");

        using var recording = _xBar.Record(destination, fs, new StockPriceSerialiser(), stoppingToken);

        await Task.Delay(5000);
        // Dispose stops recording, we're effectively recording for 5 seconds here
        recording.Dispose();

        await recording.MessageLoop;
    }
}
