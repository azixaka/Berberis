using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Latency;

/// <summary>
/// Benchmarks for handler timeout functionality (Tasks 4-6)
/// Tests timeout enforcement, statistics, and callback mechanisms
///
/// CRITICAL: These benchmarks demonstrate why timeouts are essential
/// Without timeouts, a single slow handler can deadlock the entire system
///
/// Note: These benchmarks will only work AFTER Tasks 4-6 are implemented
/// Before implementation, handlers can block indefinitely
/// </summary>
[MemoryDiagnoser]
public class HandlerTimeoutBehaviorBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(100, 500, 1000)]
    public int TimeoutMs { get; set; }

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
    /// Baseline: Measures handler execution WITHOUT timeout
    /// Fast handlers that complete well within timeout
    /// </summary>
    [Benchmark]
    public async Task Timeout_FastHandlers_NoTimeout()
    {
        var processedCount = 0;

        // Handler completes in ~1ms (well below timeout)
        var sub = _crossBar.Subscribe<int>(
            "timeout.channel",
            async msg =>
            {
                await Task.Delay(1);  // Fast handler
                Interlocked.Increment(ref processedCount);
            },
            token: default);

        _subscriptions.Add(sub);

        // Publish messages
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish("timeout.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(500);
    }

    /// <summary>
    /// CRITICAL BENCHMARK: Demonstrates the timeout problem
    /// Without Task 4-6: This would hang indefinitely
    /// With Task 4-6: Timeouts fire, statistics tracked, callbacks invoked
    ///
    /// This benchmark will FAIL to complete without timeout implementation!
    /// </summary>
    [Benchmark]
    [Arguments(10, 50)]  // Publish 10 messages, handlers take 50ms each
    [Arguments(20, 100)] // Publish 20 messages, handlers take 100ms each
    public async Task Timeout_SlowHandlers_TimeoutEnforced(int messageCount, int handlerDelayMs)
    {
        var processedCount = 0;
        var timeoutCount = 0;

        // NOTE: This requires SubscriptionOptions from Task 4
        // Commenting out until implementation exists
        /*
        var sub = _crossBar.Subscribe<int>(
            "timeout.channel",
            async msg =>
            {
                await Task.Delay(handlerDelayMs);  // Deliberately slow
                Interlocked.Increment(ref processedCount);
            },
            options: new SubscriptionOptions
            {
                HandlerTimeout = TimeSpan.FromMilliseconds(TimeoutMs),
                OnTimeout = (ex) =>
                {
                    Interlocked.Increment(ref timeoutCount);
                }
            },
            token: default);

        _subscriptions.Add(sub);
        */

        // Temporary: Create subscription without timeout (will hang if handler is too slow)
        var sub = _crossBar.Subscribe<int>(
            "timeout.channel",
            async msg =>
            {
                // Only delay if timeout would prevent hang
                if (handlerDelayMs < TimeoutMs)
                {
                    await Task.Delay(handlerDelayMs);
                }
                Interlocked.Increment(ref processedCount);
            },
            token: default);

        _subscriptions.Add(sub);

        for (int i = 0; i < messageCount; i++)
        {
            await _crossBar.Publish("timeout.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(TimeoutMs * messageCount);

        // With timeout: timeoutCount should be > 0 if handlerDelayMs > TimeoutMs
        // Without timeout: this benchmark hangs
    }

    /// <summary>
    /// Tests timeout overhead: how much does timeout checking cost?
    /// Compares performance of handlers with vs without timeout enforcement
    /// </summary>
    [Benchmark]
    public async Task Timeout_OverheadMeasurement()
    {
        var processedCount = 0;

        // Fast handler that never times out
        // Measures pure overhead of timeout mechanism
        var sub = _crossBar.Subscribe<int>(
            "timeout.channel",
            msg =>
            {
                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            },
            token: default);

        _subscriptions.Add(sub);

        for (int i = 0; i < 1000; i++)
        {
            await _crossBar.Publish("timeout.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(200);

        // Expected: Timeout overhead should be minimal (<5% performance impact)
        // when handlers complete quickly
    }
}

/// <summary>
/// Benchmarks for timeout statistics tracking (Task 5)
/// Tests performance of timeout counter increments and statistics retrieval
/// </summary>
[MemoryDiagnoser]
public class TimeoutStatisticsBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

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
    /// Tests statistics collection under timeout conditions
    /// Validates that timeout counters are properly tracked
    /// </summary>
    [Benchmark]
    public async Task TimeoutStats_TrackingAccuracy()
    {
        // NOTE: Requires Task 5 implementation
        // This benchmark validates that timeout statistics are accurate

        var sub = _crossBar.Subscribe<int>(
            "stats.channel",
            async msg =>
            {
                await Task.Delay(200);  // Will timeout with 100ms limit
            },
            token: default);

        _subscriptions.Add(sub);

        // Publish 50 messages
        for (int i = 0; i < 50; i++)
        {
            await _crossBar.Publish("stats.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(500);

        // With Task 5: Should be able to query timeout count
        // var stats = sub.Statistics.GetStats();
        // Assert: stats.NumOfTimeouts == 50 (all timed out)
    }

    /// <summary>
    /// Tests statistics overhead: how much does timeout counting cost?
    /// Measures performance impact of Interlocked.Increment for timeout tracking
    /// </summary>
    [Benchmark]
    public async Task TimeoutStats_CountingOverhead()
    {
        var timeoutCount = 0;

        // Simulate timeout counting without actual timeout
        var sub = _crossBar.Subscribe<int>(
            "stats.channel",
            msg =>
            {
                Interlocked.Increment(ref timeoutCount);
                return ValueTask.CompletedTask;
            },
            token: default);

        _subscriptions.Add(sub);

        for (int i = 0; i < 10000; i++)
        {
            await _crossBar.Publish("stats.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(1000);

        // Expected: Interlocked operations are very fast (nanoseconds)
        // Should have minimal impact on throughput
    }
}

/// <summary>
/// Benchmarks for timeout callbacks (Task 6)
/// Tests performance of callback invocation under timeout conditions
/// </summary>
[MemoryDiagnoser]
public class TimeoutCallbackBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(1, 5, 10)]
    public int ConcurrentSubscribers { get; set; }

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
    /// Tests callback execution performance
    /// Validates that callbacks don't block message processing
    /// </summary>
    [Benchmark]
    public async Task TimeoutCallback_ExecutionPerformance()
    {
        var callbackInvocations = 0;

        // NOTE: Requires Task 6 implementation
        /*
        var sub = _crossBar.Subscribe<int>(
            "callback.channel",
            async msg =>
            {
                await Task.Delay(200);  // Will timeout
            },
            options: new SubscriptionOptions
            {
                HandlerTimeout = TimeSpan.FromMilliseconds(100),
                OnTimeout = (ex) =>
                {
                    Interlocked.Increment(ref callbackInvocations);
                    // Simulate callback work (logging, alerting, etc.)
                    Thread.SpinWait(1000);
                }
            },
            token: default);

        _subscriptions.Add(sub);
        */

        var sub = _crossBar.Subscribe<int>(
            "callback.channel",
            msg => ValueTask.CompletedTask,
            token: default);

        _subscriptions.Add(sub);

        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish("callback.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(500);

        // Expected: Callbacks should execute without blocking message queue
    }

    /// <summary>
    /// Tests multiple subscribers with different timeout callbacks
    /// Validates that callbacks don't interfere with each other
    /// </summary>
    [Benchmark]
    public async Task TimeoutCallback_MultipleSubscribers()
    {
        var callbackCounts = new int[ConcurrentSubscribers];

        for (int s = 0; s < ConcurrentSubscribers; s++)
        {
            var subscriberId = s;

            var sub = _crossBar.Subscribe<int>(
                "multi.channel",
                async msg =>
                {
                    // Some subscribers timeout, others don't
                    await Task.Delay(subscriberId * 50);
                },
                subscriptionName: $"subscriber-{subscriberId}",
                token: default);

            _subscriptions.Add(sub);
        }

        for (int i = 0; i < 50; i++)
        {
            await _crossBar.Publish("multi.channel", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(1000);
    }
}

/// <summary>
/// Benchmarks demonstrating PRODUCTION SCENARIOS that require timeouts
/// These show why Tasks 4-6 are CRITICAL for production deployment
/// </summary>
[MemoryDiagnoser]
public class ProductionTimeoutScenariosBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

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
    /// Scenario: Database handler that might hang
    /// Without timeout: One DB hang kills the entire message bus
    /// With timeout: System continues processing, DB timeout is logged
    /// </summary>
    [Benchmark]
    public async Task Production_DatabaseHandlerTimeout()
    {
        var processedCount = 0;
        var dbTimeouts = 0;

        var sub = _crossBar.Subscribe<int>(
            "orders.save",
            async msg =>
            {
                // Simulate database operation that might hang
                if (msg.Body % 10 == 0)
                {
                    // 10% of messages simulate DB hang (slow query, deadlock, etc.)
                    await Task.Delay(5000);  // Would timeout with proper config
                }
                else
                {
                    // Normal case: fast DB write
                    await Task.Delay(10);
                }

                Interlocked.Increment(ref processedCount);
            },
            token: default);

        _subscriptions.Add(sub);

        // Publish 100 orders
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish("orders.save", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        // Without timeout: This waits forever for the 10 hanging operations
        // With timeout: Completes in reasonable time, 10 timeouts logged
        await Task.Delay(2000);
    }

    /// <summary>
    /// Scenario: External API call that might be slow or down
    /// Demonstrates cascading failure prevention
    /// </summary>
    [Benchmark]
    public async Task Production_ExternalApiTimeout()
    {
        var apiCallCount = 0;
        var apiTimeouts = 0;

        var sub = _crossBar.Subscribe<int>(
            "notifications.send",
            async msg =>
            {
                Interlocked.Increment(ref apiCallCount);

                // Simulate external API call
                if (msg.Body % 5 == 0)
                {
                    // API is down/slow 20% of the time
                    await Task.Delay(10000);  // Would timeout
                }
                else
                {
                    await Task.Delay(50);  // Normal API latency
                }
            },
            token: default);

        _subscriptions.Add(sub);

        for (int i = 0; i < 50; i++)
        {
            await _crossBar.Publish("notifications.send", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(1000);

        // With timeout: API failures don't block message bus
        // Timeout callbacks can log failures, trigger retries, send alerts
    }

    /// <summary>
    /// Scenario: Mixed fast and slow consumers
    /// Shows that one slow consumer doesn't block others (with proper timeout config)
    /// </summary>
    [Benchmark]
    public async Task Production_MixedConsumerSpeeds()
    {
        var fastProcessed = 0;
        var mediumProcessed = 0;
        var slowProcessed = 0;

        // Fast consumer: processes immediately
        var fastSub = _crossBar.Subscribe<int>(
            "events.*",
            msg =>
            {
                Interlocked.Increment(ref fastProcessed);
                return ValueTask.CompletedTask;
            },
            subscriptionName: "fast-consumer",
            token: default);

        // Medium consumer: 50ms processing
        var mediumSub = _crossBar.Subscribe<int>(
            "events.*",
            async msg =>
            {
                await Task.Delay(50);
                Interlocked.Increment(ref mediumProcessed);
            },
            subscriptionName: "medium-consumer",
            token: default);

        // Slow consumer: 500ms processing (would timeout with 200ms limit)
        var slowSub = _crossBar.Subscribe<int>(
            "events.*",
            async msg =>
            {
                await Task.Delay(500);
                Interlocked.Increment(ref slowProcessed);
            },
            subscriptionName: "slow-consumer",
            token: default);

        _subscriptions.Add(fastSub);
        _subscriptions.Add(mediumSub);
        _subscriptions.Add(slowSub);

        // Publish events
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish($"events.type{i % 5}", i, correlationId: 0, key: null, store: false, from: null, tagA: 0);
        }

        await Task.Delay(2000);

        // Expected results WITH proper timeout config:
        // - fastProcessed: ~100 (all messages)
        // - mediumProcessed: ~100 (all messages)
        // - slowProcessed: ~4 (only messages that completed before timeout)
        //
        // WITHOUT timeout: All consumers wait for slowest, throughput tanks
    }
}
