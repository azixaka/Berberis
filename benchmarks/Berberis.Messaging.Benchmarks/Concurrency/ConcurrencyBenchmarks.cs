using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Concurrency;

/// <summary>
/// Benchmarks for concurrent operations
/// Tests thread-safety performance characteristics
/// </summary>
[MemoryDiagnoser]
public class ConcurrencyBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;

    [Params(2, 4, 8)]
    public int ConcurrentPublishers { get; set; }

    private const int MessagesPerPublisher = 100;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "concurrent.channel",
            msg => ValueTask.CompletedTask,
            default);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Concurrency_MultiplePublishers()
    {
        var tasks = new Task[ConcurrentPublishers];

        for (int p = 0; p < ConcurrentPublishers; p++)
        {
            var publisherId = p;
            tasks[p] = Task.Run(async () =>
            {
                var msg = BenchmarkHelpers.CreateMessage(publisherId);
                for (int i = 0; i < MessagesPerPublisher; i++)
                {
                    await _crossBar.Publish("concurrent.channel", msg, store: false);
                }
            });
        }

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Benchmarks for concurrent subscription operations
/// Tests creating multiple subscriptions concurrently
/// </summary>
[MemoryDiagnoser]
public class ConcurrentSubscriberBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [Params(5, 10, 20)]
    public int SubscriberCount { get; set; }

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

    [Benchmark]
    public async Task Concurrency_CreateMultipleSubscribers()
    {
        var tasks = new Task<ISubscription>[SubscriberCount];

        for (int i = 0; i < SubscriberCount; i++)
        {
            var subId = i;
            tasks[i] = Task.Run(() =>
            {
                return _crossBar.Subscribe<int>(
                    "concurrent.channel",
                    msg => ValueTask.CompletedTask,
                    subscriptionName: $"sub-{subId}",
                    token: default);
            });
        }

        var subs = await Task.WhenAll(tasks);
        _subscriptions.AddRange(subs);
    }
}

/// <summary>
/// Benchmarks for mixed concurrent operations
/// Tests realistic scenarios with concurrent publish/subscribe
/// </summary>
[MemoryDiagnoser]
public class MixedConcurrencyBenchmarks
{
    private CrossBar _crossBar = null!;
    private List<ISubscription> _subscriptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscriptions = new List<ISubscription>();

        // Pre-create some subscriptions
        for (int i = 0; i < 5; i++)
        {
            var sub = _crossBar.Subscribe<int>(
                $"channel.{i}",
                msg => ValueTask.CompletedTask,
                default);
            _subscriptions.Add(sub);
        }
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
    public async Task Concurrency_MixedPublishAndSubscribe()
    {
        var tasks = new List<Task>();

        // Publishers
        for (int p = 0; p < 4; p++)
        {
            var channelId = p;
            tasks.Add(Task.Run(async () =>
            {
                var msg = BenchmarkHelpers.CreateMessage(channelId);
                for (int i = 0; i < 50; i++)
                {
                    await _crossBar.Publish($"channel.{channelId}", msg, store: false);
                }
            }));
        }

        // New subscribers being created
        for (int s = 0; s < 3; s++)
        {
            var subId = s + 10;
            tasks.Add(Task.Run(() =>
            {
                var sub = _crossBar.Subscribe<int>(
                    $"channel.{s}",
                    msg => ValueTask.CompletedTask,
                    subscriptionName: $"new-sub-{subId}",
                    token: default);
                lock (_subscriptions)
                {
                    _subscriptions.Add(sub);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}
