namespace Berberis.Messaging;

public interface ICrossBar
{
    IReadOnlyList<CrossBar.ChannelInfo> GetChannels();

    ValueTask Publish<TBody>(string channel, TBody body, long correlationId = 0, string? key = null);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler, 
        SlowConsumerStrategy slowConsumerStrategy = SlowConsumerStrategy.SkipUpdates, int? bufferCapacity = null);

    long GetNextCorrelationId();
}