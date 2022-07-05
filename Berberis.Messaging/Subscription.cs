using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Berberis.Messaging;

public sealed partial class Subscription<TBody> : ISubscription
{
    private readonly ILogger<Subscription<TBody>> _logger;
    private readonly string _channelName;
    private readonly Channel<Message<TBody>> _channel;
    private readonly Func<Message<TBody>, ValueTask> _handleFunc;
    private Action? _disposeAction;
    private Func<IEnumerable<Message<TBody>>>? _stateFactory;
    private readonly CrossBar _crossBar;
    private readonly bool _isSystemChannel;

    internal Subscription(ILogger<Subscription<TBody>> logger,
        long id, string? subscriptionName, string channelName, int? bufferCapacity, TimeSpan conflationIntervalInterval,
        SlowConsumerStrategy slowConsumerStrategy,
        Func<Message<TBody>, ValueTask> handleFunc,
        Action disposeAction,
        Func<IEnumerable<Message<TBody>>>? stateFactory,
        CrossBar crossBar, bool isSystemChannel)
    {
        _logger = logger;

        Name = string.IsNullOrEmpty(subscriptionName) ? $"[{id}]" : $"{subscriptionName}-[{id}]";

        _channelName = channelName;
        ConflationInterval = conflationIntervalInterval;
        SlowConsumerStrategy = slowConsumerStrategy;
        _handleFunc = handleFunc;
        _disposeAction = disposeAction;
        _stateFactory = stateFactory;
        _crossBar = crossBar;
        _isSystemChannel = isSystemChannel;

        SubscribedOn = DateTime.UtcNow;

        _channel = bufferCapacity.HasValue
            ? Channel.CreateBounded<Message<TBody>>(new BoundedChannelOptions(bufferCapacity.Value)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true, // RunReadLoopAsync is the only path reading off the channel
                SingleWriter = false, // Publisher writes to all Subscriptions and many publishers can do that simultaneously
                AllowSynchronousContinuations = false
            })
            : Channel.CreateUnbounded<Message<TBody>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        Statistics = new StatsTracker();
    }

    public string Name { get; }
    public SlowConsumerStrategy SlowConsumerStrategy { get; }
    public StatsTracker Statistics { get; }
    public DateTime SubscribedOn { get; init; }
    public TimeSpan ConflationInterval { get; init; }
    public Task MessageLoop { get; private set; }

    internal bool TryWrite(Message<TBody> message)
    {
        var success = _channel.Writer.TryWrite(message);

        if (success)
        {
            Statistics.IncNumOfEnqueuedMessages();
        }

        return success;
    }

    internal bool TryFail(Exception ex) => _channel.Writer.TryComplete(ex);

    internal void StartSubscription(CancellationToken token) =>
        MessageLoop = RunReadLoopAsync(token);

    private async Task RunReadLoopAsync(CancellationToken token)
    {
        //TODO: keep track of the last message seqid / timestamp sent on this subscription to prevent sending new update before or while sending the state!

        await Task.Yield();
        await SendState();

        Dictionary<string, Message<TBody>>? localState = null;
        Dictionary<string, Message<TBody>>? localStateBacking = null;
        SemaphoreSlim? semaphore = null;
        Task? flusherTask = null;

        if (ConflationInterval > TimeSpan.Zero)
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
                var latencyTicks = Statistics.RecordLatencyAndInterDequeueTime(message.InceptionTicks);
                Statistics.IncNumOfDequeuedMessages();

                if (!_isSystemChannel && _crossBar.MessageTracingEnabled)
                {
                    _ = _crossBar.PublishSystem(_crossBar.TracingChannel,
                                     new MessageTrace
                                     {
                                         OpType = OpType.SubscriptionDequeue,
                                         MessageKey = message.Key,
                                         CorrelationId = message.CorrelationId,
                                         From = message.From,
                                         Channel = _channelName,
                                         SubscriptionName = Name,
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
            float flushTookMs = 0;

            while (!token.IsCancellationRequested)
            {
                var delay = ConflationInterval.Subtract(TimeSpan.FromMilliseconds(flushTookMs));

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, token);
                }

                var startTicks = StatsTracker.GetTicks();

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

                flushTookMs = StatsTracker.TicksToTimeMs(StatsTracker.GetTicks() - startTicks);
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

        var svcTimeTicks = Statistics.RecordServiceAndInterProcessTime(beforeServiceTicks);
        Statistics.IncNumOfProcessedMessages();

        if (!_isSystemChannel && _crossBar.MessageTracingEnabled)
        {
            _ = _crossBar.PublishSystem(_crossBar.TracingChannel,
                             new MessageTrace
                             {
                                 OpType = OpType.SubscriptionProcessed,
                                 MessageKey = message.Key,
                                 CorrelationId = message.CorrelationId,
                                 From = message.From,
                                 Channel = _channelName,
                                 SubscriptionName = Name,
                                 Ticks = StatsTracker.GetTicks()
                             });
        }

        if (_crossBar.PublishLoggingEnabled && _logger.IsEnabled(LogLevel.Trace))
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
