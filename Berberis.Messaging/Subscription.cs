using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Berberis.Messaging;

public sealed partial class Subscription<TBody> : ISubscription
{
    private readonly ILogger<Subscription<TBody>> _logger;
    private readonly Channel<Message<TBody>> _channel;
    private readonly Func<Message<TBody>, ValueTask> _handleFunc;
    private Action? _disposeAction;

    internal Subscription(ILogger<Subscription<TBody>> logger,
        long id, int? boundedCapacity, SlowConsumerStrategy slowConsumerStrategy,
        Func<Message<TBody>, ValueTask> handleFunc,
        Action disposeAction)
    {
        _logger = logger;
        Id = id;
        SlowConsumerStrategy = slowConsumerStrategy;
        _handleFunc = handleFunc;
        _disposeAction = disposeAction;

        _channel = boundedCapacity.HasValue
            ? Channel.CreateBounded<Message<TBody>>(new BoundedChannelOptions(boundedCapacity.Value)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            })
            : Channel.CreateUnbounded<Message<TBody>>(new UnboundedChannelOptions
            {
                SingleReader = false, // Subscription is thread-safe, so we don't know how consumer is using it
                SingleWriter = false, // Publisher currently writes to all Subscriptions
                AllowSynchronousContinuations = false
            });

        Statistics = new StatsTracker();
    }

    public long Id { get; }

    public SlowConsumerStrategy SlowConsumerStrategy { get; }

    public StatsTracker Statistics { get; }

    internal bool TryWrite(Message<TBody> message)
    {
        var success = _channel.Writer.TryWrite(message);

        if (success)
        {
            Statistics.IncNumOfMessages();
        }

        return success;
    }

    internal bool TryFail(Exception ex) => _channel.Writer.TryComplete(ex);

    public async Task RunReadLoopAsync(CancellationToken token = default)
    {
        await Task.Yield();

        while (await _channel.Reader.WaitToReadAsync(token))
        {
            while (_channel.Reader.TryRead(out var message))
            {
                var latencyTicks = Statistics.RecordLatency(message.InceptionTicks);
                Statistics.DecNumOfMessages();

                var beforeServiceTicks = StatsTracker.GetTicks();

                var task = _handleFunc(message);
                if (!task.IsCompleted)
                    await task;

                var svcTimeTicks = Statistics.RecordServiceTime(beforeServiceTicks);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    LogStats(message.Id,
                        StatsTracker.TicksToTimeMs(svcTimeTicks),
                        StatsTracker.TicksToTimeMs(latencyTicks));
                }
            }
        }
    }

    public void Dispose()
    {
        var disposeAction = Interlocked.Exchange(ref _disposeAction, null);
        if (disposeAction != null)
        {
            disposeAction();
            _channel.Writer.TryComplete();
        }
    }

    [LoggerMessage(0, LogLevel.Trace, "Processed message [{msgId}] in {svcTime:N2}ms, latency {latency:N2}ms")]
    partial void LogStats(long msgId, float svcTime, float latency);
}
