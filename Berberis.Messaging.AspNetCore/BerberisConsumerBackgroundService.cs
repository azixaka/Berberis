using Microsoft.Extensions.Hosting;

namespace Berberis.Messaging.AspNetCore;

public class BerberisConsumerBackgroundService : BackgroundService
{
    private readonly ICrossBar _crossBar;
    private readonly IEnumerable<IBerberisConsumer> _consumers;

    public BerberisConsumerBackgroundService(ICrossBar crossBar, IEnumerable<IBerberisConsumer> consumers)
    {
        _crossBar = crossBar;
        _consumers = consumers;
    }

    protected override  Task ExecuteAsync(CancellationToken token)
    {
        return Task.WhenAll(_consumers.Select(async consumer =>
        {
            using var subscription = consumer.Start(_crossBar);
            await subscription.RunReadLoopAsync(token);
        }));
    }
}