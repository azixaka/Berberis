using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Wildcards;

/// <summary>
/// Benchmarks for wildcard pattern matching
/// Tests pattern matching performance overhead
/// </summary>
[MemoryDiagnoser]
public class WildcardBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _wildcardSub = null!;
    private ISubscription _directSub = null!;
    private Message<int> _message;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();

        // Wildcard subscription
        _wildcardSub = _crossBar.Subscribe<int>(
            "orders.*",
            msg => ValueTask.CompletedTask,
            default);

        // Direct subscription for comparison
        _directSub = _crossBar.Subscribe<int>(
            "orders.new",
            msg => ValueTask.CompletedTask,
            default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _wildcardSub?.Dispose();
        _directSub?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Wildcard_DirectChannelPublish()
    {
        await _crossBar.Publish("orders.new", _message, store: false);
    }

    [Benchmark]
    public async Task Wildcard_SingleLevelMatch()
    {
        await _crossBar.Publish("orders.cancelled", _message, store: false);
    }
}

/// <summary>
/// Benchmarks for recursive wildcard pattern
/// Tests performance of '>' operator
/// </summary>
[MemoryDiagnoser]
public class RecursiveWildcardBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _recursiveWildcard = null!;
    private Message<int> _message;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();

        // Recursive wildcard subscription
        _recursiveWildcard = _crossBar.Subscribe<int>(
            "orders.>",
            msg => ValueTask.CompletedTask,
            default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _recursiveWildcard?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Wildcard_RecursiveMatch_Level2()
    {
        await _crossBar.Publish("orders.new", _message, store: false);
    }

    [Benchmark]
    public async Task Wildcard_RecursiveMatch_Level3()
    {
        await _crossBar.Publish("orders.shipped.fedex", _message, store: false);
    }

    [Benchmark]
    public async Task Wildcard_RecursiveMatch_Level5()
    {
        await _crossBar.Publish("orders.a.b.c.d", _message, store: false);
    }
}

/// <summary>
/// Benchmarks for wildcard matching with many channels
/// Tests scalability of pattern matching
/// </summary>
[MemoryDiagnoser]
public class ManyChannelsWildcardBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _wildcardSub = null!;
    private List<ISubscription> _channelSubs = null!;
    private Message<int> _message;

    [Params(10, 50, 100)]
    public int ChannelCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _channelSubs = new List<ISubscription>();

        // Create many channels
        for (int i = 0; i < ChannelCount; i++)
        {
            var sub = _crossBar.Subscribe<int>(
                $"orders.type{i}",
                msg => ValueTask.CompletedTask,
                default);
            _channelSubs.Add(sub);
        }

        // Wildcard subscription matching all
        _wildcardSub = _crossBar.Subscribe<int>(
            "orders.*",
            msg => ValueTask.CompletedTask,
            default);

        _message = BenchmarkHelpers.CreateMessage(42);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _wildcardSub?.Dispose();
        foreach (var sub in _channelSubs)
        {
            sub?.Dispose();
        }
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task Wildcard_MatchWithManyChannels()
    {
        await _crossBar.Publish("orders.type25", _message, store: false);
    }
}
