using Microsoft.Extensions.Hosting;

namespace Berberis.Messaging.AspNetCore;

public class BerberisConsumerBackgroundService : BackgroundService
{
    private readonly ICrossBar _xBar;
    private readonly IEnumerable<IBerberisConsumer> _consumers;

    public BerberisConsumerBackgroundService(ICrossBar xBar, IEnumerable<IBerberisConsumer> consumers)
    {
        _xBar = xBar;
        _consumers = consumers;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var subscriptions = _consumers.Select(c => c.Start(_xBar)).ToArray();
        try
        {
            await Task.WhenAll(subscriptions.Select(s => s.RunReadLoopAsync(token)));
        }
        finally
        {
            foreach (var sub in subscriptions)
            {
                sub.Dispose();
            }
        }
    }
}