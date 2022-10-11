using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class MaxProducerService : BackgroundService
{
    private readonly ICrossBar _xBar;

    public MaxProducerService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000, stoppingToken);

        const string destination = "number.inc";

        while (!stoppingToken.IsCancellationRequested)
        {
            var p1 = Task.Run(() =>
            {
                int key = 0;

                for (long i = 0; i < long.MaxValue; i++)
                {
                    _xBar.Publish(destination, i, key, key: key.ToString(), store: true, from: "MaxProducerService");
                    if (key++ > 100)
                        key = 0;

                    //Thread.SpinWait(10000);
                }
            }, stoppingToken);

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