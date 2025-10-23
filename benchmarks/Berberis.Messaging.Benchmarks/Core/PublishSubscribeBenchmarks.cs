using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Core;

/// <summary>
/// Benchmarks for basic publish/subscribe operations
/// Measures the fundamental performance of the message broker
/// </summary>
[MemoryDiagnoser]
public class PublishSubscribeBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<int> _message;
    private int _receivedCount;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _receivedCount = 0;

        // Setup subscription
        _subscription = _crossBar.Subscribe<int>(
            "benchmark.channel",
            msg =>
            {
                _receivedCount++;
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

    [Benchmark(Baseline = true)]
    public async Task Publish_SingleMessage()
    {
        await _crossBar.Publish("benchmark.channel", _message, store: false);
    }

    [Benchmark]
    public async Task Publish_And_Receive_SingleMessage()
    {
        var receivedBefore = _receivedCount;
        await _crossBar.Publish("benchmark.channel", _message, store: false);

        // Spin-wait for message to be received (typically completes immediately)
        var deadline = DateTime.UtcNow.AddMilliseconds(100);
        while (_receivedCount == receivedBefore && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }
    }

    [Benchmark]
    public async Task Publish_100Messages()
    {
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish("benchmark.channel", _message, store: false);
        }
    }
}

/// <summary>
/// Benchmarks for multiple subscribers scenario
/// Tests fan-out performance
/// </summary>
[MemoryDiagnoser]
public class MultipleSubscribersBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;
    private Message<int> _message;

    [Params(1, 3, 10)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscriptions = new List<ISubscription>();

        // Create multiple subscribers
        for (int i = 0; i < SubscriberCount; i++)
        {
            var sub = _crossBar.Subscribe<int>(
                "benchmark.channel",
                msg => ValueTask.CompletedTask, default);
            _subscriptions.Add(sub);
        }

        _message = BenchmarkHelpers.CreateMessage(42);
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

    [Benchmark]
    public async Task Publish_ToMultipleSubscribers()
    {
        await _crossBar.Publish("benchmark.channel", _message, store: false);
    }
}

/// <summary>
/// Benchmarks for different message payload sizes
/// Tests impact of payload size on performance
/// </summary>
[MemoryDiagnoser]
public class MessageSizeBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<BenchmarkHelpers.BenchmarkData> _smallMessage;
    private Message<BenchmarkHelpers.BenchmarkData> _mediumMessage;
    private Message<BenchmarkHelpers.BenchmarkData> _largeMessage;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<BenchmarkHelpers.BenchmarkData>(
            "benchmark.channel",
            msg => ValueTask.CompletedTask, default);

        _smallMessage = BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateSmallPayload(1));
        _mediumMessage = BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateMediumPayload(1));
        _largeMessage = BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateLargePayload(1));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Publish_SmallPayload()
    {
        await _crossBar.Publish("benchmark.channel", _smallMessage, store: false);
    }

    [Benchmark]
    public async Task Publish_MediumPayload_1KB()
    {
        await _crossBar.Publish("benchmark.channel", _mediumMessage, store: false);
    }

    [Benchmark]
    public async Task Publish_LargePayload_10KB()
    {
        await _crossBar.Publish("benchmark.channel", _largeMessage, store: false);
    }
}
