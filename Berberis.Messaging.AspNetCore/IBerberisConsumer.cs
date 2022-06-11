namespace Berberis.Messaging.AspNetCore;

public interface IBerberisConsumer
{
    ISubscription Start(ICrossBar crossBar);
}

public abstract class BerberisConsumer<T>: IBerberisConsumer
{
    /// <summary>
    /// Gets channel name this consumer is supposed to subscribe on.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Current subscription handle.
    /// </summary>
    protected ISubscription Subscription { get; private set; } = null!;
    
    protected BerberisConsumer(string channel)
    {
        Channel = channel;
    }

    protected abstract ValueTask Consume(Message<T> message);

    public ISubscription Start(ICrossBar crossBar)
    {
        return Subscription = crossBar.Subscribe<T>(Channel, Consume);
    }
}