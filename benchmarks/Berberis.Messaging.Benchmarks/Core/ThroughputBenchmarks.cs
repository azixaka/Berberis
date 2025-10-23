using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Core;

/// <summary>
/// Benchmarks for message throughput
/// Measures sustained message processing rate
/// </summary>
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<int> _message;

    [Params(1000, 10000, 100000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "throughput.channel",
            msg => ValueTask.CompletedTask,
            null, false, SlowConsumerStrategy.SkipUpdates,
            MessageCount * 2, // Ensure buffer won't overflow
            TimeSpan.Zero, default, default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Publish_SustainedThroughput()
    {
        for (int i = 0; i < MessageCount; i++)
        {
            await _crossBar.Publish("throughput.channel", _message, store: false);
        }
    }
}

/// <summary>
/// Benchmarks for multi-channel throughput
/// Tests performance with messages distributed across channels
/// </summary>
[MemoryDiagnoser]
public class MultiChannelThroughputBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;
    private Message<int> _message;

    [Params(5, 10, 20)]
    public int ChannelCount { get; set; }

    private const int MessagesPerChannel = 1000;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscriptions = new List<ISubscription>();

        // Create subscriptions for each channel
        for (int i = 0; i < ChannelCount; i++)
        {
            var sub = _crossBar.Subscribe<int>(
                $"channel.{i}",
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
    public async Task Publish_MultipleChannels()
    {
        for (int i = 0; i < MessagesPerChannel; i++)
        {
            for (int ch = 0; ch < ChannelCount; ch++)
            {
                await _crossBar.Publish($"channel.{ch}", _message, store: false);
            }
        }
    }
}

/// <summary>
/// Benchmarks for concurrent publishing
/// Tests thread-safety overhead and scalability
/// </summary>
[MemoryDiagnoser]
public class ConcurrentPublishThroughputBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<int> _message;

    [Params(2, 4, 8)]
    public int ConcurrentPublishers { get; set; }

    private const int MessagesPerPublisher = 1000;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "concurrent.channel",
            msg => ValueTask.CompletedTask,
            null, false, SlowConsumerStrategy.SkipUpdates,
            MessagesPerPublisher * ConcurrentPublishers * 2,
            TimeSpan.Zero, default, default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Publish_ConcurrentPublishers()
    {
        var tasks = new Task[ConcurrentPublishers];

        for (int p = 0; p < ConcurrentPublishers; p++)
        {
            tasks[p] = Task.Run(async () =>
            {
                for (int i = 0; i < MessagesPerPublisher; i++)
                {
                    await _crossBar.Publish("concurrent.channel", _message, store: false);
                }
            });
        }

        await Task.WhenAll(tasks);
    }
}
