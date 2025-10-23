using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Stateful;

/// <summary>
/// Benchmarks for stateful channel operations
/// Tests state storage and retrieval performance
/// </summary>
[MemoryDiagnoser]
public class StatefulChannelBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "stateful.channel",
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
    public async Task Stateful_PublishWithKey_StoreMessage()
    {
        var msg = BenchmarkHelpers.CreateMessage(42, key: "key-1");
        await _crossBar.Publish("stateful.channel", msg, store: true);
    }

    [Benchmark]
    public async Task Stateful_UpdateSameKey_100Times()
    {
        for (int i = 0; i < 100; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: "key-1");
            await _crossBar.Publish("stateful.channel", msg, store: true);
        }
    }

    [Benchmark]
    public async Task Stateful_Store100DifferentKeys()
    {
        for (int i = 0; i < 100; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: $"key-{i}");
            await _crossBar.Publish("stateful.channel", msg, store: true);
        }
    }
}

/// <summary>
/// Benchmarks for state retrieval
/// Tests GetChannelState performance
/// </summary>
[MemoryDiagnoser]
public class StateRetrievalBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;

    [Params(10, 100, 1000)]
    public int StateSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "state.channel",
            msg => ValueTask.CompletedTask,
            default);

        // Populate state
        for (int i = 0; i < StateSize; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: $"key-{i}");
            await _crossBar.Publish("state.channel", msg, store: true);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public List<Message<int>> Stateful_GetChannelState()
    {
        return _crossBar.GetChannelState<int>("state.channel").ToList();
    }
}

/// <summary>
/// Benchmarks for large state scenarios
/// Tests performance under high state volume
/// </summary>
[MemoryDiagnoser]
public class LargeStateBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;
    private const int LargeStateSize = 10000;

    [GlobalSetup]
    public async Task Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
        _subscription = _crossBar.Subscribe<int>(
            "large.channel",
            msg => ValueTask.CompletedTask,
            default);

        // Populate large state
        for (int i = 0; i < LargeStateSize; i++)
        {
            var msg = BenchmarkHelpers.CreateMessage(i, key: $"key-{i}");
            await _crossBar.Publish("large.channel", msg, store: true);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    [Benchmark]
    public async Task LargeState_UpdateExistingKey()
    {
        var msg = BenchmarkHelpers.CreateMessage(999999, key: "key-5000");
        await _crossBar.Publish("large.channel", msg, store: true);
    }

    [Benchmark]
    public async Task LargeState_AddNewKey()
    {
        var msg = BenchmarkHelpers.CreateMessage(999999, key: "new-key");
        await _crossBar.Publish("large.channel", msg, store: true);
    }

    [Benchmark]
    public List<Message<int>> LargeState_GetFullState()
    {
        return _crossBar.GetChannelState<int>("large.channel").ToList();
    }
}
