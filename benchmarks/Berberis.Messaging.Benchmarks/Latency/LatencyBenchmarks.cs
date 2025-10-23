using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Latency;

/// <summary>
/// Benchmarks for message latency
/// Measures time from publish to handler execution
/// </summary>
[MemoryDiagnoser]
public class LatencyBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<int> _message;
    private TaskCompletionSource<bool> _received = null!;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _received = new TaskCompletionSource<bool>();

        _subscription = _crossBar.Subscribe<int>(
            "latency.channel",
            msg =>
            {
                _received.TrySetResult(true);
                return ValueTask.CompletedTask;
            }, default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Latency_PublishToReceive()
    {
        _received = new TaskCompletionSource<bool>();
        await _crossBar.Publish("latency.channel", _message, store: false);
        await _received.Task;
    }
}

/// <summary>
/// Benchmarks for handler execution overhead
/// Tests impact of different handler types
/// </summary>
[MemoryDiagnoser]
public class HandlerExecutionBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _syncSubscription = null!;
    private ISubscription _asyncSubscription = null!;
    private ISubscription _delayedSubscription = null!;
    private Message<int> _message;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _message = BenchmarkHelpers.CreateMessage(42);

        // Synchronous handler (completes immediately)
        _syncSubscription = _crossBar.Subscribe<int>(
            "sync.channel",
            msg => ValueTask.CompletedTask, default);

        // Async handler (yields then completes)
        _asyncSubscription = _crossBar.Subscribe<int>(
            "async.channel",
            async msg => await Task.Yield(), default);

        // Delayed handler (simulates I/O)
        _delayedSubscription = _crossBar.Subscribe<int>(
            "delayed.channel",
            async msg => await Task.Delay(1), default);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _syncSubscription?.Dispose();
        _asyncSubscription?.Dispose();
        _delayedSubscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Handler_Synchronous()
    {
        await _crossBar.Publish("sync.channel", _message, store: false);
    }

    [Benchmark]
    public async Task Handler_AsyncYield()
    {
        await _crossBar.Publish("async.channel", _message, store: false);
    }

    [Benchmark]
    public async Task Handler_With1msDelay()
    {
        await _crossBar.Publish("delayed.channel", _message, store: false);
    }
}

/// <summary>
/// Benchmarks for latency distribution
/// Measures percentiles over many operations
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 100, warmupCount: 10)]
public class LatencyDistributionBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<int> _message;
    private ConcurrentBag<long> _latencies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _latencies = new ConcurrentBag<long>();

        _subscription = _crossBar.Subscribe<int>(
            "latency.channel",
            msg =>
            {
                // Note: InceptionTicks is internal, so we can't access it directly
                // This benchmark measures overall latency without tracking individual message ticks
                var latency = System.Diagnostics.Stopwatch.GetTimestamp();
                _latencies.Add(latency);
                return ValueTask.CompletedTask;
            }, default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Measure_100Messages_LatencyDistribution()
    {
        _latencies.Clear();

        for (int i = 0; i < 100; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i);
            await _crossBar.Publish("latency.channel", msg, store: false);
        }

        // Wait for all to be received
        while (_latencies.Count < 100)
        {
            await Task.Yield();
        }
    }
}
