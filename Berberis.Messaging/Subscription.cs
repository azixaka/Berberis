using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Berberis.Messaging;

public sealed partial class Subscription<TBody> : ISubscription
{
    private readonly ILogger<Subscription<TBody>> _logger;
    private readonly string _channelName;
    private readonly int _conflationIntervalMilliseconds;
    private readonly Channel<Message<TBody>> _channel;
    private readonly Func<Message<TBody>, ValueTask> _handleFunc;
    private Action? _disposeAction;
    private Func<IEnumerable<Message<TBody>>>? _stateFactory;
    private readonly CrossBar _crossBar;
    private readonly bool _isSystemChannel;

    internal Subscription(ILogger<Subscription<TBody>> logger,
        long id, string channelName, int? bufferCapacity, int conflationIntervalMilliseconds,
        SlowConsumerStrategy slowConsumerStrategy,
        Func<Message<TBody>, ValueTask> handleFunc,
        Action disposeAction,
        Func<IEnumerable<Message<TBody>>>? stateFactory,
        CrossBar crossBar, bool isSystemChannel)
    {
        _logger = logger;
        Id = id;
        _channelName = channelName;
        _conflationIntervalMilliseconds = conflationIntervalMilliseconds;
        SlowConsumerStrategy = slowConsumerStrategy;
        _handleFunc = handleFunc;
        _disposeAction = disposeAction;
        _stateFactory = stateFactory;
        _crossBar = crossBar;
        _isSystemChannel = isSystemChannel;
        _channel = bufferCapacity.HasValue
            ? Channel.CreateBounded<Message<TBody>>(new BoundedChannelOptions(bufferCapacity.Value)
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
        //TODO: keep track of the last message seqid / timestamp sent on this subscription to prevent sending new update before or while sending the state!

        await Task.Yield();
        await SendState();

        Dictionary<string, Message<TBody>>? localState = null;
        Dictionary<string, Message<TBody>>? localStateBacking = null;
        SemaphoreSlim? semaphore = null;
        Task? flusherTask = null;

        if (_conflationIntervalMilliseconds != Timeout.Infinite)
        {
            localState = new Dictionary<string, Message<TBody>>();
            localStateBacking = new Dictionary<string, Message<TBody>>();
            semaphore = new SemaphoreSlim(1, 1);
            flusherTask = FlusherLoop();
        }

        while (await _channel.Reader.WaitToReadAsync(token))
        {
            while (_channel.Reader.TryRead(out var message))
            {
                var latencyTicks = Statistics.RecordLatency(message.InceptionTicks);
                Statistics.DecNumOfMessages();

                if (!_isSystemChannel && _crossBar.TracingEnabled)
                {
                    _ = _crossBar.PublishSystem(_crossBar.TracingChannel,
                                     new MessageTrace
                                     {
                                         OpType = OpType.SubscriptionDequeue,
                                         MessageId = message.Id,
                                         MessageKey = message.Key,
                                         CorrelationId = message.CorrelationId,
                                         From = message.From,
                                         Channel = _channelName,
                                         SubscriptionId = Id,
                                         Ticks = StatsTracker.GetTicks()
                                     });
                }

                if (localState == null || string.IsNullOrEmpty(message.Key))
                {
                    var task = ProcessMessage(message, latencyTicks);
                    if (!task.IsCompleted)
                        await task;
                }
                else
                {
                    if (!semaphore!.Wait(0))
                        await semaphore!.WaitAsync();

                    try
                    {
                        localState[message.Key] = message;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }

                //if conflation enabled add to the local state here
                //if we're over the period then flush data
                //otherwise schedule a flush in a period residual time
            }
        }

        if (flusherTask != null)
        {
            await flusherTask;
            semaphore!.Dispose();
        }

        async Task FlusherLoop()
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_conflationIntervalMilliseconds, token);

                if (!semaphore!.Wait(0))
                    await semaphore!.WaitAsync(token);

                Dictionary<string, Message<TBody>>? state = null;

                try
                {
                    if (localState.Count > 0)
                    {
                        state = localState;
                        (localState, localStateBacking) = (localStateBacking, localState);
                    }
                }
                finally
                {
                    semaphore.Release();
                }

                if (state != null)
                {
                    try
                    {
                        foreach (var (_, message) in state)
                        {
                            var task = ProcessMessage(message, 0); //todo: latency for logging!
                            if (!task.IsCompleted)
                                await task;
                        }
                    }
                    finally
                    {
                        state.Clear();
                    }
                }
            }
        }
    }

    private async Task SendState()
    {
        var stateFactory = Interlocked.Exchange(ref _stateFactory, null);

        if (stateFactory != null)
        {
            foreach (var message in stateFactory())
            {
                var task = ProcessMessage(message, 0);
                if (!task.IsCompleted)
                    await task;
            }
        }
    }

    private async Task ProcessMessage(Message<TBody> message, long latencyTicks)
    {
        var beforeServiceTicks = StatsTracker.GetTicks();

        var task = _handleFunc(message);
        if (!task.IsCompleted)
            await task;

        var svcTimeTicks = Statistics.RecordServiceTime(beforeServiceTicks);

        if (!_isSystemChannel && _crossBar.TracingEnabled)
        {
            _ = _crossBar.PublishSystem(_crossBar.TracingChannel,
                             new MessageTrace
                             {
                                 OpType = OpType.SubscriptionProcessed,
                                 MessageId = message.Id,
                                 MessageKey = message.Key,
                                 CorrelationId = message.CorrelationId,
                                 From = message.From,
                                 Channel = _channelName,
                                 SubscriptionId = Id,
                                 Ticks = StatsTracker.GetTicks()
                             });
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            LogStats(message.Id,
                StatsTracker.TicksToTimeMs(svcTimeTicks),
                StatsTracker.TicksToTimeMs(latencyTicks));
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
