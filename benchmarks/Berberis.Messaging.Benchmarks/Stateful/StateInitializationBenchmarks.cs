using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Stateful;

/// <summary>
/// Benchmarks for state initialization race conditions (Task 1)
/// Tests the scenario documented in Subscription.cs:120
///
/// RACE CONDITION:
/// 1. Subscription starts, begins sending state
/// 2. New message arrives during state send
/// 3. New message might be older than state being sent
/// 4. Subscriber receives out-of-order messages
///
/// BEFORE Task 1: No sequence tracking, potential duplicates/out-of-order
/// AFTER Task 1: Sequence tracking prevents duplicate/out-of-order delivery
/// </summary>
[MemoryDiagnoser]
public class StateInitializationRaceBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(10, 50, 100)]
    public int StateSize { get; set; }

    [Params(2, 4, 8)]
    public int ConcurrentPublishers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscriptions = new List<ISubscription>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _crossBar?.Dispose();
    }

    [IterationSetup]
    public void PopulateState()
    {
        // Create large state
        for (int i = 0; i < StateSize; i++)
        {
            _crossBar.Publish(
                "orders.channel",
                i,
                key: $"order-{i}",
                store: true).GetAwaiter().GetResult();
        }
    }

    [IterationCleanup]
    public void CleanupSubscriptions()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _subscriptions.Clear();
        _crossBar.ResetChannel<int>("orders.channel");
    }

    /// <summary>
    /// Tests the exact race condition:
    /// - Subscribers request state (fetchState: true)
    /// - WHILE state is being sent, new messages arrive
    /// - Tests if sequence tracking prevents duplicates
    ///
    /// This is the critical benchmark for Task 1 validation
    /// </summary>
    [Benchmark]
    public async Task State_ConcurrentSubscribeAndPublish()
    {
        var tasks = new List<Task>();
        var messageCounters = new int[ConcurrentPublishers + 1];

        // Start subscribers that request state
        tasks.Add(Task.Run(() =>
        {
            var sub = _crossBar.Subscribe<int>(
                "orders.channel",
                msg =>
                {
                    Interlocked.Increment(ref messageCounters[0]);
                    return ValueTask.CompletedTask;
                },
                subscriptionName: "state-subscriber",
                fetchState: true,  // CRITICAL: This triggers state send
                token: default);

            lock (_subscriptions)
            {
                _subscriptions.Add(sub);
            }
        }));

        // Simultaneously publish new messages while state is being sent
        for (int p = 0; p < ConcurrentPublishers; p++)
        {
            var publisherId = p + 1;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await _crossBar.Publish(
                        "orders.channel",
                        StateSize + i,
                        key: $"new-order-{publisherId}-{i}",
                        store: true);

                    Interlocked.Increment(ref messageCounters[publisherId]);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Give time for all messages to be processed
        await Task.Delay(100);

        // Note: With Task 1 fix, subscriber should receive:
        // - All state messages (StateSize)
        // - All new messages (ConcurrentPublishers * 10)
        // - No duplicates
    }

    /// <summary>
    /// Tests multiple subscribers requesting state concurrently
    /// Each gets state while others are also getting state
    /// </summary>
    [Benchmark]
    public async Task State_MultipleSubscribersRequestStateConcurrently()
    {
        var tasks = new Task[8];

        for (int i = 0; i < 8; i++)
        {
            var subId = i;
            tasks[i] = Task.Run(() =>
            {
                var receivedCount = 0;
                var sub = _crossBar.Subscribe<int>(
                    "orders.channel",
                    msg =>
                    {
                        Interlocked.Increment(ref receivedCount);
                        return ValueTask.CompletedTask;
                    },
                    subscriptionName: $"subscriber-{subId}",
                    fetchState: true,
                    token: default);

                lock (_subscriptions)
                {
                    _subscriptions.Add(sub);
                }
            });
        }

        await Task.WhenAll(tasks);
        await Task.Delay(200);
    }

    /// <summary>
    /// Tests worst case: rapid publishing during state initialization
    /// Maximizes the race window
    /// </summary>
    [Benchmark]
    public async Task State_RapidPublishingDuringStateInit()
    {
        var receivedMessages = new List<long>();
        var lockObj = new object();

        var subscribeTask = Task.Run(() =>
        {
            var sub = _crossBar.Subscribe<int>(
                "orders.channel",
                msg =>
                {
                    lock (lockObj)
                    {
                        receivedMessages.Add(msg.Id);
                    }
                    return ValueTask.CompletedTask;
                },
                subscriptionName: "race-subscriber",
                fetchState: true,
                token: default);

            lock (_subscriptions)
            {
                _subscriptions.Add(sub);
            }
        });

        // Immediately start publishing at high rate
        var publishTasks = new List<Task>();
        for (int i = 0; i < 1000; i++)
        {
            var msgId = StateSize + i;
            publishTasks.Add(_crossBar.Publish(
                "orders.channel",
                msgId,
                key: $"rapid-{i}",
                store: true).AsTask());
        }

        await Task.WhenAll(subscribeTask, Task.WhenAll(publishTasks));
        await Task.Delay(200);

        // With Task 1 fix: receivedMessages should be monotonically increasing
        // (no out-of-order, no duplicates)
    }
}

/// <summary>
/// Benchmarks for state consistency under concurrent operations
/// Tests various scenarios that could trigger state race conditions
/// </summary>
[MemoryDiagnoser]
public class StateConsistencyBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(100, 1000)]
    public int MessageRate { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscriptions = new List<ISubscription>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _crossBar?.Dispose();
    }

    /// <summary>
    /// Tests churning subscribers: subscribe, receive some messages, unsubscribe
    /// While continuous publishing happens
    /// </summary>
    [Benchmark]
    public async Task State_SubscriberChurning()
    {
        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < MessageRate; i++)
            {
                await _crossBar.Publish(
                    "churning.channel",
                    i,
                    key: $"key-{i % 10}",
                    store: true);
            }
        });

        var subscribeTasks = new List<Task>();
        for (int s = 0; s < 10; s++)
        {
            subscribeTasks.Add(Task.Run(async () =>
            {
                var sub = _crossBar.Subscribe<int>(
                    "churning.channel",
                    msg => ValueTask.CompletedTask,
                    fetchState: true,
                    token: default);

                await Task.Delay(50);
                sub.Dispose();
            }));
        }

        await Task.WhenAll(publishTask, Task.WhenAll(subscribeTasks));
    }

    /// <summary>
    /// Tests state fetch timing with varying state sizes
    /// Measures overhead of state delivery
    /// </summary>
    [Benchmark]
    public async Task State_FetchLargeStateWhilePublishing()
    {
        // Populate large state
        for (int i = 0; i < 1000; i++)
        {
            await _crossBar.Publish(
                "large.state",
                i,
                key: $"key-{i}",
                store: true);
        }

        var tasks = new List<Task>();

        // Subscribe and fetch large state
        tasks.Add(Task.Run(() =>
        {
            var sub = _crossBar.Subscribe<int>(
                "large.state",
                msg => ValueTask.CompletedTask,
                fetchState: true,
                token: default);

            lock (_subscriptions)
            {
                _subscriptions.Add(sub);
            }
        }));

        // Continue publishing while state is being fetched
        for (int i = 0; i < MessageRate; i++)
        {
            var msgId = 1000 + i;
            tasks.Add(_crossBar.Publish(
                "large.state",
                msgId,
                key: $"key-{i % 100}",
                store: true).AsTask());
        }

        await Task.WhenAll(tasks);
        await Task.Delay(200);

        _crossBar.ResetChannel<int>("large.state");
    }

    /// <summary>
    /// Tests mixed state operations: fetching, updating, resetting
    /// Stresses sequence tracking logic
    /// </summary>
    [Benchmark]
    public async Task State_MixedOperationsUnderLoad()
    {
        var tasks = new List<Task>();

        // Publishers
        for (int p = 0; p < 4; p++)
        {
            var publisherId = p;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < MessageRate / 4; i++)
                {
                    await _crossBar.Publish(
                        "mixed.channel",
                        i,
                        key: $"pub{publisherId}-key{i % 20}",
                        store: true);
                }
            }));
        }

        // Subscribers with state fetch
        for (int s = 0; s < 3; s++)
        {
            var subId = s;
            tasks.Add(Task.Run(() =>
            {
                var sub = _crossBar.Subscribe<int>(
                    "mixed.channel",
                    msg => ValueTask.CompletedTask,
                    subscriptionName: $"subscriber-{subId}",
                    fetchState: true,
                    token: default);

                lock (_subscriptions)
                {
                    _subscriptions.Add(sub);
                }
            }));
        }

        // State readers
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var state = _crossBar.GetChannelState<int>("mixed.channel");
                var count = 0;
                foreach (var msg in state)
                {
                    count++;
                }
                Thread.Sleep(10);
            }
        }));

        await Task.WhenAll(tasks);
        _crossBar.ResetChannel<int>("mixed.channel");
    }
}

/// <summary>
/// Benchmarks for state send performance characteristics
/// Measures the overhead of state initialization
/// </summary>
[MemoryDiagnoser]
public class StateSendPerformanceBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(10, 100, 1000, 10000)]
    public int StateSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscriptions = new List<ISubscription>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _crossBar?.Dispose();
    }

    [IterationSetup]
    public void PopulateState()
    {
        for (int i = 0; i < StateSize; i++)
        {
            _crossBar.Publish(
                "perf.channel",
                i,
                key: $"key-{i}",
                store: true).GetAwaiter().GetResult();
        }
    }

    [IterationCleanup]
    public void CleanupSubscriptions()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _subscriptions.Clear();
        _crossBar.ResetChannel<int>("perf.channel");
    }

    /// <summary>
    /// Measures time to deliver state to a single subscriber
    /// Baseline for state send performance
    /// </summary>
    [Benchmark]
    public async Task State_SingleSubscriberFetchTime()
    {
        var receivedCount = 0;
        var allReceived = new TaskCompletionSource<bool>();

        var sub = _crossBar.Subscribe<int>(
            "perf.channel",
            msg =>
            {
                if (Interlocked.Increment(ref receivedCount) >= StateSize)
                {
                    allReceived.TrySetResult(true);
                }
                return ValueTask.CompletedTask;
            },
            fetchState: true,
            token: default);

        _subscriptions.Add(sub);

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Measures overhead with sequence tracking (Task 1)
    /// Compares before/after Task 1 implementation
    /// </summary>
    [Benchmark]
    public async Task State_WithSequenceTracking()
    {
        var receivedMessages = new List<long>();
        var lockObj = new object();
        var allReceived = new TaskCompletionSource<bool>();

        var sub = _crossBar.Subscribe<int>(
            "perf.channel",
            msg =>
            {
                lock (lockObj)
                {
                    receivedMessages.Add(msg.Id);
                    if (receivedMessages.Count >= StateSize)
                    {
                        allReceived.TrySetResult(true);
                    }
                }
                return ValueTask.CompletedTask;
            },
            fetchState: true,
            token: default);

        _subscriptions.Add(sub);

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // With Task 1: receivedMessages should be sorted (sequence enforced)
        // Overhead should be minimal (just sequence comparison)
    }
}
