using Berberis.Messaging;
using Berberis.Recorder;
using static Berberis.SampleApp.MaxConsumerService;

namespace Berberis.SampleApp;

public sealed class MaxProducerService : BackgroundService
{
    private readonly ICrossBar _xBar;
    private readonly ILogger<MaxProducerService> _logger;

    public MaxProducerService(ICrossBar xBar, ILogger<MaxProducerService> logger)
    {
        _xBar = xBar;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000);

        var destination = "number.inc";

        //using var fs = File.OpenRead(@"c:\temp\numbers.stream");

        //var player = Player<long>.Create(fs, new NumberSerialiser());

        //var reporter = Task.Run(async () =>
        //{
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        var rStats = player.Stats;

        //        var statsText = $"MPS: {rStats.MessagesPerSecond:N0}; BPS: {rStats.BytesPerSecond:N0}; TB: {rStats.TotalBytes:N0}; SVC: {rStats.AvgServiceTime:N4};";

        //        _logger.LogInformation("{statsText}", statsText);

        //        await Task.Delay(1000);
        //    }
        //});

        //await foreach (var msg in player.MessagesAsync(stoppingToken))
        //{
        //    await _xBar.Publish(destination, msg);
        //}

        while (!stoppingToken.IsCancellationRequested)
        {
            var p1 = Task.Run(() =>
            {
                int key = 0;

                for (long i = 0; i < long.MaxValue; i++)
                {
                    _xBar.Publish(destination, i);

                    //_xBar.Publish(destination, i, key, key: key.ToString(), store: true, from: "MaxProducerService");
                    //if (key++ > 100)
                    //    key = 0;

                    Thread.SpinWait(30);
                }
            });

            //var p2 = Task.Run(() =>
            //{
            //    for (long i = 0; i < long.MaxValue; i++)
            //    {
            //        _xBar.Publish(destination, i);
            //        //await Task.Delay(100);
            //    }
            //});

            await Task.WhenAll(p1);
        }
    }
}