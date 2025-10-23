using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Conflation;

/// <summary>
/// Benchmarks for message conflation
/// Tests conflation performance and effectiveness
/// </summary>
[MemoryDiagnoser]
public class ConflationBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _noConflation = null!;
    private ISubscription _withConflation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();

        // No conflation
        _noConflation = _crossBar.Subscribe<int>(
            "no-conflation.channel",
            msg => ValueTask.CompletedTask,
            default);

        // With conflation
        _withConflation = _crossBar.Subscribe<int>(
            "conflation.channel",
            msg => ValueTask.CompletedTask,
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(100),
            token: default);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _noConflation?.Dispose();
        _withConflation?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Conflation_NoConflation_100Updates()
    {
        for (int i = 0; i < 100; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: "price");
            await _crossBar.Publish("no-conflation.channel", msg, store: false);
        }
    }

    [Benchmark]
    public async Task Conflation_WithConflation_100Updates()
    {
        for (int i = 0; i < 100; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: "price");
            await _crossBar.Publish("conflation.channel", msg, store: false);
        }

        // Wait for flush
        await Task.Delay(150);
    }
}

/// <summary>
/// Benchmarks for conflation overhead
/// Tests performance cost of conflation mechanism
/// </summary>
[MemoryDiagnoser]
public class ConflationOverheadBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;

    [Params(10, 50, 100)]
    public int FlushIntervalMs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();

        _subscription = _crossBar.Subscribe<int>(
            "conflation.channel",
            msg => ValueTask.CompletedTask,
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(FlushIntervalMs),
            token: default);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Conflation_OverheadWith100Messages()
    {
        for (int i = 0; i < 100; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: $"key-{i % 10}");
            await _crossBar.Publish("conflation.channel", msg, store: false);
        }

        // Wait for flush
        await Task.Delay(FlushIntervalMs + 50);
    }
}
