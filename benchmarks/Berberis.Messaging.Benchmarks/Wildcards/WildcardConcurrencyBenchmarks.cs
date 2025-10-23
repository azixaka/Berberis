using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Wildcards;

/// <summary>
/// Benchmarks for wildcard subscription race conditions (Task 3)
/// Tests the scenario documented in CrossBar.cs:239
///
/// RACE CONDITION:
/// 1. Thread A: Subscribes with pattern "orders.*"
/// 2. Thread B: Publishes to "orders.new" (creates channel)
/// 3. Thread A: Registers wildcard subscription
/// 4. Result: Message from step 2 might be missed
///
/// This benchmark demonstrates the eventual consistency model
/// </summary>
[MemoryDiagnoser]
public class WildcardRaceConditionBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(2, 4, 8)]
    public int ConcurrentOperations { get; set; }

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
    /// Tests the exact race condition scenario:
    /// Wildcard subscriptions being created WHILE channels are being published to
    ///
    /// This demonstrates the race window where messages can be lost
    /// </summary>
    [Benchmark]
    public async Task Wildcard_ConcurrentSubscribeAndPublish()
    {
        var tasks = new List<Task>();
        var receivedMessages = 0;

        // Start wildcard subscriptions
        for (int i = 0; i < ConcurrentOperations; i++)
        {
            var subId = i;
            tasks.Add(Task.Run(() =>
            {
                var sub = _crossBar.Subscribe<int>(
                    "orders.*",  // Wildcard pattern
                    msg =>
                    {
                        Interlocked.Increment(ref receivedMessages);
                        return ValueTask.CompletedTask;
                    },
                    subscriptionName: $"wildcard-sub-{subId}",
                    token: default);

                lock (_subscriptions)
                {
                    _subscriptions.Add(sub);
                }
            }));
        }

        // Simultaneously publish to matching channels
        for (int i = 0; i < ConcurrentOperations * 10; i++)
        {
            var msgId = i;
            tasks.Add(Task.Run(async () =>
            {
                await _crossBar.Publish(
                    $"orders.channel{msgId % ConcurrentOperations}",
                    msgId,
                    correlationId: 0,
                    key: null,
                    store: false,
                    from: null,
                    tagA: 0);
            }));
        }

        await Task.WhenAll(tasks);

        // Note: receivedMessages will be less than expected due to race condition
        // This is documented behavior, not a bug
    }

    /// <summary>
    /// Tests wildcard subscription to pre-existing channels (no race)
    /// This is the expected usage pattern
    /// </summary>
    [Benchmark]
    public async Task Wildcard_SubscribeAfterChannelsExist()
    {
        var receivedMessages = 0;

        // Pre-create channels by publishing first
        for (int i = 0; i < ConcurrentOperations * 10; i++)
        {
            await _crossBar.Publish(
                $"trades.channel{i % ConcurrentOperations}",
                i,
                correlationId: 0,
                key: null,
                store: false,
                from: null,
                tagA: 0);
        }

        // Now subscribe with wildcard - should catch all existing channels
        var tasks = new Task[ConcurrentOperations];
        for (int i = 0; i < ConcurrentOperations; i++)
        {
            var subId = i;
            tasks[i] = Task.Run(() =>
            {
                var sub = _crossBar.Subscribe<int>(
                    "trades.*",
                    msg =>
                    {
                        Interlocked.Increment(ref receivedMessages);
                        return ValueTask.CompletedTask;
                    },
                    subscriptionName: $"wildcard-sub-{subId}",
                    token: default);

                lock (_subscriptions)
                {
                    _subscriptions.Add(sub);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Give time for subscriptions to be fully registered
        await Task.Delay(100);

        // Now publish - should be received by all subscribers
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish(
                $"trades.channel{i % ConcurrentOperations}",
                i,
                correlationId: 0,
                key: null,
                store: false,
                from: null,
                tagA: 0);
        }
    }

    /// <summary>
    /// Tests multiple wildcard patterns competing for the same channels
    /// Measures overhead of pattern matching under contention
    /// </summary>
    [Benchmark]
    public async Task Wildcard_MultiplePatternsConcurrent()
    {
        var tasks = new List<Task>();

        // Create subscriptions with different wildcard patterns
        var patterns = new[]
        {
            "market.*.prices",
            "market.>",
            "market.*.trades",
            "market.equity.*",
            "market.fx.*"
        };

        foreach (var pattern in patterns)
        {
            for (int i = 0; i < ConcurrentOperations; i++)
            {
                var subId = i;
                var currentPattern = pattern;
                tasks.Add(Task.Run(() =>
                {
                    var sub = _crossBar.Subscribe<int>(
                        currentPattern,
                        msg => ValueTask.CompletedTask,
                        subscriptionName: $"sub-{currentPattern}-{subId}",
                        token: default);

                    lock (_subscriptions)
                    {
                        _subscriptions.Add(sub);
                    }
                }));
            }
        }

        // Publish to various channels that match different patterns
        var channels = new[]
        {
            "market.equity.prices",
            "market.fx.prices",
            "market.equity.trades",
            "market.fx.trades",
            "market.crypto.prices"
        };

        for (int i = 0; i < 100; i++)
        {
            foreach (var channel in channels)
            {
                tasks.Add(_crossBar.Publish(channel, i, correlationId: 0, key: null, store: false, from: null, tagA: 0).AsTask());
            }
        }

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Benchmarks for wildcard subscription with stateful channels
/// Tests the interaction of wildcard patterns with MessageStore operations
/// </summary>
[MemoryDiagnoser]
public class WildcardStatefulBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(5, 10)]
    public int ChannelCount { get; set; }

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
    public void PopulateChannels()
    {
        // Create multiple stateful channels with state
        for (int c = 0; c < ChannelCount; c++)
        {
            for (int i = 0; i < 10; i++)
            {
                _crossBar.Publish(
                    $"prices.symbol{c}",
                    i,
                    correlationId: 0,
                    key: $"key-{i}",
                    store: true,
                    from: null,
                    tagA: 0).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>
    /// Tests wildcard subscription with fetchState=true
    /// Should fetch state from all matching channels
    /// </summary>
    [Benchmark]
    public async Task Wildcard_FetchStateFromMultipleChannels()
    {
        var receivedCount = 0;

        var sub = _crossBar.Subscribe<int>(
            "prices.*",
            msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            },
            fetchState: true,  // Should receive state from all matching channels
            token: default);

        _subscriptions.Add(sub);

        // Wait for state to be delivered
        await Task.Delay(100);
    }

    /// <summary>
    /// Tests concurrent wildcard subscriptions with state fetch
    /// Stress tests FindMatchingChannels + MessageStore access
    /// </summary>
    [Benchmark]
    public async Task Wildcard_ConcurrentStateSubscriptions()
    {
        var tasks = new Task[8];

        for (int i = 0; i < 8; i++)
        {
            var subId = i;
            tasks[i] = Task.Run(() =>
            {
                var sub = _crossBar.Subscribe<int>(
                    "prices.*",
                    msg => ValueTask.CompletedTask,
                    subscriptionName: $"state-sub-{subId}",
                    fetchState: true,
                    token: default);

                lock (_subscriptions)
                {
                    _subscriptions.Add(sub);
                }
            });
        }

        await Task.WhenAll(tasks);
        await Task.Delay(100);
    }

    /// <summary>
    /// Tests wildcard pattern with concurrent state updates
    /// Demonstrates the overhead of wildcard routing with stateful channels
    /// </summary>
    [Benchmark]
    public async Task Wildcard_ConcurrentStateUpdates()
    {
        // Create wildcard subscriber first
        var receivedCount = 0;
        var sub = _crossBar.Subscribe<int>(
            "prices.*",
            msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            },
            token: default);

        _subscriptions.Add(sub);

        // Now concurrently update state across multiple channels
        var tasks = new List<Task>();
        for (int c = 0; c < ChannelCount; c++)
        {
            var channelId = c;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    await _crossBar.Publish(
                        $"prices.symbol{channelId}",
                        i,
                        correlationId: 0,
                        key: $"key-{i % 10}",
                        store: true,
                        from: null,
                        tagA: 0);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Benchmarks for recursive wildcard (>) performance with concurrency
/// Tests the most expensive pattern matching scenario under concurrent load
/// </summary>
[MemoryDiagnoser]
public class RecursiveWildcardConcurrencyBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(10, 50)]
    public int ChannelDepth { get; set; }

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
    /// Tests recursive wildcard with deeply nested channel names
    /// Measures pattern matching overhead
    /// </summary>
    [Benchmark]
    public async Task RecursiveWildcard_DeepChannelHierarchy()
    {
        var receivedCount = 0;

        // Subscribe with recursive wildcard
        var sub = _crossBar.Subscribe<int>(
            "market.>",
            msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            },
            token: default);

        _subscriptions.Add(sub);

        // Publish to channels at various depths
        var tasks = new List<Task>();
        for (int depth = 1; depth <= ChannelDepth; depth++)
        {
            var channelName = "market";
            for (int level = 0; level < depth; level++)
            {
                channelName += $".level{level}";
            }

            var currentChannel = channelName;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await _crossBar.Publish(currentChannel, i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests multiple recursive wildcards at different hierarchy levels
    /// Worst case for pattern matching performance
    /// </summary>
    [Benchmark]
    public async Task RecursiveWildcard_MultipleOverlappingPatterns()
    {
        var tasks = new List<Task>();

        // Create overlapping recursive wildcard subscriptions
        var patterns = new[]
        {
            "market.>",
            "market.equity.>",
            "market.fx.>",
            "market.crypto.>"
        };

        foreach (var pattern in patterns)
        {
            for (int i = 0; i < 3; i++)
            {
                var subId = i;
                var currentPattern = pattern;
                tasks.Add(Task.Run(() =>
                {
                    var sub = _crossBar.Subscribe<int>(
                        currentPattern,
                        msg => ValueTask.CompletedTask,
                        subscriptionName: $"sub-{currentPattern}-{subId}",
                        token: default);

                    lock (_subscriptions)
                    {
                        _subscriptions.Add(sub);
                    }
                }));
            }
        }

        // Publish to various depths
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_crossBar.Publish("market.equity.us.tech.prices", i, correlationId: 0, key: null, store: false, from: null, tagA: 0).AsTask());
            tasks.Add(_crossBar.Publish("market.fx.eurusd.prices", i, correlationId: 0, key: null, store: false, from: null, tagA: 0).AsTask());
            tasks.Add(_crossBar.Publish("market.crypto.btc.prices", i, correlationId: 0, key: null, store: false, from: null, tagA: 0).AsTask());
        }

        await Task.WhenAll(tasks);
    }
}
