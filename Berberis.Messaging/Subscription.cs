using Berberis.Messaging.Statistics;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Berberis.Messaging;

/// <summary>Typed subscription to channel messages.</summary>
public sealed partial class Subscription<TBody> : ISubscription
{
    private readonly ILogger<Subscription<TBody>> _logger;
    private readonly Channel<Message<TBody>> _channel;
    private readonly Func<Message<TBody>, ValueTask> _handleFunc;
    private Action? _disposeAction;
    private IReadOnlyCollection<Func<IEnumerable<Message<TBody>>>>? _stateFactories;
    private readonly CrossBar _crossBar;
    private readonly bool _isSystemChannel;

    private int _isSuspended;
    private TaskCompletionSource _resumeProcessingSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _lastSentSequenceId = -1;
    private readonly TimeSpan? _handlerTimeout;
    private readonly Action<Exceptions.HandlerTimeoutException>? _onTimeoutAction;
    private long _timeoutCount;

    internal Subscription(ILogger<Subscription<TBody>> logger,
        long id, string? subscriptionName, string channelName, int? bufferCapacity,
        TimeSpan conflationIntervalInterval,
        SlowConsumerStrategy slowConsumerStrategy,
        Func<Message<TBody>, ValueTask> handleFunc,
        Action disposeAction,
        IReadOnlyCollection<Func<IEnumerable<Message<TBody>>>>? stateFactories,
        CrossBar crossBar, bool isSystemChannel, bool isWildcard,
        StatsOptions statsOptions,
        SubscriptionOptions? options = null)
    {
        _logger = logger;
        Name = string.IsNullOrEmpty(subscriptionName) ? $"[{id}]" : $"{subscriptionName}-[{id}]";
        MessageBodyType = typeof(TBody);
        IsWildcard = isWildcard;

        ChannelName = channelName;
        ConflationInterval = conflationIntervalInterval;
        SlowConsumerStrategy = slowConsumerStrategy;
        _handleFunc = handleFunc;
        _disposeAction = disposeAction;
        _stateFactories = stateFactories;
        _crossBar = crossBar;
        _isSystemChannel = isSystemChannel;

        // Extract timeout settings from options
        _handlerTimeout = options?.HandlerTimeout;
        _onTimeoutAction = options?.OnTimeout;

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

        Statistics = new StatsTracker(statsOptions);
    }

    /// <summary>Subscription name.</summary>
    public string Name { get; }
    /// <summary>Backpressure handling strategy.</summary>
    public SlowConsumerStrategy SlowConsumerStrategy { get; }
    /// <summary>Performance statistics tracker.</summary>
    public StatsTracker Statistics { get; }
    /// <summary>Subscription creation time.</summary>
    public DateTime SubscribedOn { get; init; }
    /// <summary>Message conflation interval.</summary>
    public TimeSpan ConflationInterval { get; init; }
    /// <summary>Message processing loop task.</summary>
    public Task MessageLoop { get; private set; } = null!;
    /// <summary>Message body type.</summary>
    public Type MessageBodyType { get; }

    /// <summary>True if wildcard subscription.</summary>
    public bool IsWildcard { get; init; }

    /// <summary>Channel or pattern name.</summary>
    public string ChannelName { get; init; }

    /// <summary>True if detached from channel.</summary>
    public bool IsDetached { get; set; }

    /// <summary>Suspend/resume message processing.</summary>
    public bool IsProcessingSuspended
    {
        get => Volatile.Read(ref _isSuspended) == 1;
        set
        {
            // if we are trying to suspend processing and we atomically achieved that (set _isSuspended to 1 and it had 0 there previously)
            if (value && Interlocked.Exchange(ref _isSuspended, 1) == 0)
            {
                // create a new TCS and set it to the _resumeProcessingSignal field while fetching what was there previously and signal it
                // this is to prevent a potential deadlock if the message processing loop checked if it needs to suspend and then awaited
                // on a TCS referenced by the _resumeProcessingSignal that will be replaced with a new one below
                var prevSignal = Interlocked.Exchange(ref _resumeProcessingSignal, new(TaskCreationOptions.RunContinuationsAsynchronously));
                prevSignal.TrySetResult();
            }

            if (!value && Interlocked.Exchange(ref _isSuspended, 0) == 1)
            {
                _resumeProcessingSignal.TrySetResult();
            }
        }
    }

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
        await Task.Yield();
        _lastSentSequenceId = await SendState();

        Dictionary<string, Message<TBody>>? localState = null;
        Dictionary<string, Message<TBody>>? localStateBacking = null;
        SemaphoreSlim? semaphore = null;
        Task? flusherTask = null;

        if (ConflationInterval > TimeSpan.Zero)
        {
            localState = new();
            localStateBacking = new();
            semaphore = new(1, 1);
            flusherTask = FlusherLoop();
        }

        while (await _channel.Reader.WaitToReadAsync(token))
        {
            while (_channel.Reader.TryRead(out var message))
            {
                // Skip messages that were already sent during state initialization
                if (message.Id <= _lastSentSequenceId)
                {
                    _logger.LogTrace(
                        "Skipping message [{msgId}] on subscription [{sub}] - already sent in state",
                        message.Id,
                        Name);
                    continue;
                }

                var latencyTicks = Statistics.RecordLatency(message.InceptionTicks);
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
                                         Channel = ChannelName,
                                         SubscriptionName = Name,
                                         Ticks = StatsTracker.GetTicks()
                                     });
                }

                if (localState == null || string.IsNullOrEmpty(message.Key))
                {
                    // !!!!!!! THIS IS A COPY OF THE ProcessMessage METHOD body except the PostProcessMessage call HERE
                    // The ProcessMessage code is called on every update (very often) OR in each conflation cycle (less often) OR when sending initial state (once for all the data)
                    // By copying its content here, we avoid massive async state machine allocations
                    if (Volatile.Read(ref _isSuspended) == 1)
                        await _resumeProcessingSignal.Task;

                    var beforeServiceTicks = StatsTracker.GetTicks();

                    try
                    {
                        if (_handlerTimeout.HasValue)
                        {
                            // Execute with timeout
                            var task = _handleFunc(message);
                            if (!task.IsCompleted)
                                await task.AsTask().WaitAsync(_handlerTimeout.Value);
                        }
                        else
                        {
                            // No timeout - existing fast path (zero allocation)
                            var task = _handleFunc(message);
                            if (!task.IsCompleted)
                                await task;
                        }
                        // !!!!!!! THIS IS A COPY OF THE ProcessMessage METHOD HERE

                        PostProcessMessage(ref message, beforeServiceTicks, latencyTicks);
                    }
                    catch (TimeoutException)
                    {
                        // Handler timed out
                        if (_handlerTimeout.HasValue)
                        {
                            Interlocked.Increment(ref _timeoutCount);

                            _logger.LogError(
                                "Handler timeout after {TimeoutMs}ms on subscription [{SubscriptionName}], " +
                                "channel [{ChannelName}], message [{MessageId}]",
                                _handlerTimeout.Value.TotalMilliseconds,
                                Name,
                                ChannelName,
                                message.Id);

                            var timeoutEx = new Exceptions.HandlerTimeoutException(
                                Name,
                                ChannelName,
                                message.Id,
                                _handlerTimeout.Value);

                            _onTimeoutAction?.Invoke(timeoutEx);

                            Statistics.IncNumOfTimeouts();

                            // Don't call PostProcessMessage - message failed to process
                            // Subscription continues processing next message
                        }
                    }
                    catch (Exception ex)
                    {
                        // Other exceptions - existing error handling
                        _logger.LogError(ex,
                            "Handler exception on subscription [{SubscriptionName}], " +
                            "channel [{ChannelName}], message [{MessageId}]",
                            Name,
                            ChannelName,
                            message.Id);

                        // Continue processing
                    }
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

    private async Task<long> SendState()
    {
        long maxSequenceId = -1;

        if (_stateFactories != null)
            foreach (var stateFactory in _stateFactories)
            {
                foreach (var message in stateFactory())
                {
                    maxSequenceId = Math.Max(maxSequenceId, message.Id);

                    var task = ProcessMessage(message, 0);
                    if (!task.IsCompleted)
                        await task;
                }
            }

        if (maxSequenceId > -1)
        {
            _logger.LogInformation(
                "Sent state for subscription [{sub}], last seq ID: {seqId}",
                Name,
                maxSequenceId);
        }

        return maxSequenceId;
    }

    private async Task ProcessMessage(Message<TBody> message, long latencyTicks)
    {
        if (Volatile.Read(ref _isSuspended) == 1)
            await _resumeProcessingSignal.Task;

        var beforeServiceTicks = StatsTracker.GetTicks();

        try
        {
            if (_handlerTimeout.HasValue)
            {
                // Execute with timeout
                var task = _handleFunc(message);
                if (!task.IsCompleted)
                    await task.AsTask().WaitAsync(_handlerTimeout.Value);
            }
            else
            {
                // No timeout - existing fast path (zero allocation)
                var task = _handleFunc(message);
                if (!task.IsCompleted)
                    await task;
            }

            PostProcessMessage(ref message, beforeServiceTicks, latencyTicks);
        }
        catch (TimeoutException)
        {
            // Handler timed out
            if (_handlerTimeout.HasValue)
            {
                Interlocked.Increment(ref _timeoutCount);

                _logger.LogError(
                    "Handler timeout after {TimeoutMs}ms on subscription [{SubscriptionName}], " +
                    "channel [{ChannelName}], message [{MessageId}]",
                    _handlerTimeout.Value.TotalMilliseconds,
                    Name,
                    ChannelName,
                    message.Id);

                var timeoutEx = new Exceptions.HandlerTimeoutException(
                    Name,
                    ChannelName,
                    message.Id,
                    _handlerTimeout.Value);

                _onTimeoutAction?.Invoke(timeoutEx);

                Statistics.IncNumOfTimeouts();

                // Don't call PostProcessMessage - message failed to process
                // Subscription continues processing next message
            }
        }
        catch (Exception ex)
        {
            // Other exceptions - existing error handling
            _logger.LogError(ex,
                "Handler exception on subscription [{SubscriptionName}], " +
                "channel [{ChannelName}], message [{MessageId}]",
                Name,
                ChannelName,
                message.Id);

            // Continue processing
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PostProcessMessage(ref Message<TBody> message, long beforeServiceTicks, long latencyTicks)
    {
        var svcTimeTicks = Statistics.RecordServiceTime(beforeServiceTicks);
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
                                 Channel = ChannelName,
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

    /// <summary>Gets handler timeout count.</summary>
    public long GetTimeoutCount() => Volatile.Read(ref _timeoutCount);

    /// <summary>Disposes subscription and stops processing.</summary>
    public void Dispose()
    {
        var disposeAction = Interlocked.Exchange(ref _disposeAction, null);
        if (disposeAction != null)
        {
            try
            {
                disposeAction();
            }
            finally
            {
                _channel.Writer.TryComplete();
            }
        }
    }

    [LoggerMessage(0, LogLevel.Trace, "Processed message [{msgId}] in {svcTime:N2}ms, latency {latency:N2}ms")]
    partial void LogStats(long msgId, float svcTime, float latency);
}
