using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Memory;

/// <summary>
/// Benchmarks for memory allocations
/// Verifies allocation-free claims for hot paths
/// </summary>
[MemoryDiagnoser]
public class AllocationBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private Message<int> _message;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "alloc.channel",
            msg => ValueTask.CompletedTask,
            default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Allocations_SinglePublish()
    {
        await _crossBar.Publish("alloc.channel", _message, store: false);
    }

    [Benchmark]
    public async Task Allocations_100Publishes()
    {
        for (int i = 0; i < 100; i++)
        {
            await _crossBar.Publish("alloc.channel", _message, store: false);
        }
    }
}

/// <summary>
/// Benchmarks for subscription creation allocations
/// Measures cold-path allocation behavior
/// </summary>
[MemoryDiagnoser]
public class SubscriptionAllocationBenchmarks
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

    [Benchmark]
    public void Allocations_CreateSubscription()
    {
        var sub = _crossBar.Subscribe<int>(
            "alloc.channel",
            msg => ValueTask.CompletedTask,
            default);
        _subscriptions.Add(sub);
    }

    [Benchmark]
    public void Allocations_CreateSubscription_WithOptions()
    {
        var sub = _crossBar.Subscribe<int>(
            "alloc.channel",
            msg => ValueTask.CompletedTask,
            subscriptionName: "test-sub",
            fetchState: false,
            conflationInterval: TimeSpan.Zero,
            statsOptions: default,
            token: default);
        _subscriptions.Add(sub);
    }
}

/// <summary>
/// Benchmarks for message structure allocations
/// Tests message creation overhead
/// </summary>
[MemoryDiagnoser]
public class MessageCreationBenchmarks
{
    [Benchmark(Baseline = true)]
    public Message<int> Allocations_CreateSimpleMessage()
    {
        return BenchmarkHelpers.CreateMessage(42);
    }

    [Benchmark]
    public Message<int> Allocations_CreateMessageWithKey()
    {
        return BenchmarkHelpers.CreateMessage(42, key: "test-key");
    }

    [Benchmark]
    public Message<BenchmarkHelpers.BenchmarkData> Allocations_CreateMessageWithComplexBody()
    {
        return BenchmarkHelpers.CreateMessage(BenchmarkHelpers.CreateSmallPayload(1));
    }
}
