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
    
    protected BerberisConsumer(string channel)
    {
        Channel = channel;
    }

    protected abstract ValueTask Consume(Message<T> message, ISubscription subscription);

    public ISubscription Start(ICrossBar crossBar)
    {
        ISubscription subscription = null!;
        // ReSharper disable once AccessToModifiedClosure
        return subscription = crossBar.Subscribe<T>(Channel, message => Consume(message, subscription));
    }
}