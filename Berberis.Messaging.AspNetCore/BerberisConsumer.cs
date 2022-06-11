namespace Berberis.Messaging.AspNetCore;

public abstract class BerberisConsumer<T>: IBerberisConsumer
{
    /// <summary>
    /// Gets channel name this consumer is supposed to subscribe on.
    /// </summary>
    public string Channel { get; }
    
    public bool FetchState { get; }
    public SlowConsumerStrategy SlowConsumerStrategy { get; }
    public int? BufferCapacity { get; }
    public int ConflationIntervalMilliseconds { get; }

    /// <summary>
    /// Current subscription handle.
    /// </summary>
    protected ISubscription Subscription { get; private set; } = null!;

    protected BerberisConsumer(string channel, bool fetchState = false,
        SlowConsumerStrategy slowConsumerStrategy = SlowConsumerStrategy.SkipUpdates, int? bufferCapacity = null,
        int conflationIntervalMilliseconds = Timeout.Infinite)
    {
        Channel = channel;
        FetchState = fetchState;
        SlowConsumerStrategy = slowConsumerStrategy;
        BufferCapacity = bufferCapacity;
        ConflationIntervalMilliseconds = conflationIntervalMilliseconds;
    }

    protected abstract ValueTask Consume(Message<T> message);

    public ISubscription Start(ICrossBar crossBar)
    {
        return Subscription = crossBar.Subscribe<T>(Channel, Consume, FetchState, SlowConsumerStrategy, BufferCapacity,
            ConflationIntervalMilliseconds);
    }
}
