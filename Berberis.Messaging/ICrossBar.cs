namespace Berberis.Messaging;

public interface ICrossBar
{
    IReadOnlyList<CrossBar.ChannelInfo> GetChannels();

    IReadOnlyCollection<CrossBar.SubscriptionInfo> GetChannelSubscriptions(string channelName);

    ValueTask Publish<TBody>(string channel, TBody body, long correlationId = 0, string? key = null, bool store = false);

    ISubscription Subscribe<TBody>(string channel, Func<Message<TBody>, ValueTask> handler,
        bool fetchState = false,
        SlowConsumerStrategy slowConsumerStrategy = SlowConsumerStrategy.SkipUpdates,
        int? bufferCapacity = null);

    long GetNextCorrelationId();
}