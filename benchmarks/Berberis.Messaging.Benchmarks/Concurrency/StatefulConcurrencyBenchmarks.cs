using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Berberis.Messaging;
using Berberis.Messaging.Benchmarks.Helpers;

namespace Berberis.Messaging.Benchmarks.Concurrency;

/// <summary>
/// Benchmarks for stateful channel concurrent operations
/// Tests MessageStore performance under concurrent write contention
///
/// CRITICAL: These benchmarks will show 3-5x improvement after Tasks 7-9
/// (replacing Dictionary+lock with ConcurrentDictionary in MessageStore)
/// </summary>
[MemoryDiagnoser]
public class StatefulConcurrencyBenchmarks
{
    private CrossBar _crossBar = null!;
    private ISubscription _subscription = null!;

    [Params(2, 4, 8, 16)]
    public int ConcurrentPublishers { get; set; }

    private const int UpdatesPerPublisher = 100;

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();

        // Subscribe to receive all updates
        _subscription = _crossBar.Subscribe<int>(
            "stateful.channel",
            msg => ValueTask.CompletedTask,
            fetchState: true,  // Fetch state on subscribe
            token: default);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription?.Dispose();
        _crossBar?.Dispose();
    }

    /// <summary>
    /// Tests concurrent updates to the SAME keys (maximum contention)
    /// This is the worst-case scenario for the current lock-based MessageStore
    /// Expected improvement: 3-5x after ConcurrentDictionary optimization
    /// </summary>
    [Benchmark]
    public async Task Stateful_ConcurrentUpdates_SameKeys()
    {
        var tasks = new Task[ConcurrentPublishers];

        for (int p = 0; p < ConcurrentPublishers; p++)
        {
            var publisherId = p;
            tasks[p] = Task.Run(async () =>
            {
                // All publishers update the same 10 keys
                for (int i = 0; i < UpdatesPerPublisher; i++)
                {
                    var key = $"key-{i % 10}";  // Only 10 keys, maximum contention
                    await _crossBar.Publish(
                        "stateful.channel",
                        publisherId * 1000 + i,
                        correlationId: 0,
                        key: key,
                        store: true,  // CRITICAL: store=true exercises MessageStore
                        from: null,
                        tagA: 0);
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests concurrent updates to DIFFERENT keys (reduced contention)
    /// Each publisher has its own set of keys
    /// Should still benefit from lock-free operations
    /// </summary>
    [Benchmark]
    public async Task Stateful_ConcurrentUpdates_DifferentKeys()
    {
        var tasks = new Task[ConcurrentPublishers];

        for (int p = 0; p < ConcurrentPublishers; p++)
        {
            var publisherId = p;
            tasks[p] = Task.Run(async () =>
            {
                // Each publisher has unique keys
                for (int i = 0; i < UpdatesPerPublisher; i++)
                {
                    var key = $"publisher-{publisherId}-key-{i}";
                    await _crossBar.Publish(
                        "stateful.channel",
                        publisherId * 1000 + i,
                        correlationId: 0,
                        key: key,
                        store: true,
                        from: null,
                        tagA: 0);
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests mixed reads and writes to MessageStore
    /// Simulates real-world scenario: some threads publishing, others fetching state
    /// </summary>
    [Benchmark]
    public async Task Stateful_MixedReadsAndWrites()
    {
        var tasks = new List<Task>();

        // Publishers (concurrent writes)
        for (int p = 0; p < ConcurrentPublishers / 2; p++)
        {
            var publisherId = p;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < UpdatesPerPublisher; i++)
                {
                    var key = $"key-{i % 20}";
                    await _crossBar.Publish(
                        "stateful.channel",
                        publisherId * 1000 + i,
                        correlationId: 0,
                        key: key,
                        store: true,
                        from: null,
                        tagA: 0);
                }
            }));
        }

        // Readers (concurrent state fetches)
        for (int r = 0; r < ConcurrentPublishers / 2; r++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < UpdatesPerPublisher; i++)
                {
                    // Read current state
                    var state = _crossBar.GetChannelState<int>("stateful.channel");
                    // Force enumeration to actually read
                    var count = 0;
                    foreach (var msg in state)
                    {
                        count++;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Benchmarks for stateful channel deletion and reset operations
/// Tests MessageStore cleanup performance under contention
/// </summary>
[MemoryDiagnoser]
public class StatefulCleanupBenchmarks
{
    private CrossBar _crossBar = null!;

    [Params(100, 1000, 10000)]
    public int StateSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _crossBar?.Dispose();
    }

    [IterationSetup]
    public void PopulateState()
    {
        // Create large state
        for (int i = 0; i < StateSize; i++)
        {
            _crossBar.Publish(
                "stateful.channel",
                i,
                correlationId: 0,
                key: $"key-{i}",
                store: true,
                from: null,
                tagA: 0).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Tests resetting a channel with large state
    /// Measures cleanup performance
    /// </summary>
    [Benchmark]
    public void Stateful_ResetLargeChannel()
    {
        _crossBar.ResetChannel<int>("stateful.channel");
    }

    /// <summary>
    /// Tests concurrent message deletion
    /// Multiple threads deleting different messages
    /// </summary>
    [Benchmark]
    public async Task Stateful_ConcurrentDeletes()
    {
        var tasks = new Task[8];
        var keysPerThread = StateSize / 8;

        for (int t = 0; t < 8; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(() =>
            {
                var start = threadId * keysPerThread;
                var end = start + keysPerThread;

                for (int i = start; i < end; i++)
                {
                    _crossBar.TryDeleteMessage<int>("stateful.channel", $"key-{i}");
                }
            });
        }

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Benchmarks for Channel initialization race condition (Task 2)
/// Tests GetOrCreateMessageStore under extreme concurrent access
/// </summary>
[MemoryDiagnoser]
public class MessageStoreInitializationBenchmarks
{
    private CrossBar _crossBar = null!;

    [Params(10, 50, 100)]
    public int ConcurrentInitializers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _crossBar = BenchmarkHelpers.CreateBenchmarkCrossBar();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _crossBar?.Dispose();
    }

    /// <summary>
    /// Tests concurrent first-time publication to a channel
    /// This triggers GetOrCreateMessageStore concurrently
    ///
    /// BEFORE Task 2: Potential double initialization
    /// AFTER Task 2: Thread-safe Lazy initialization
    /// </summary>
    [Benchmark]
    public async Task Channel_ConcurrentFirstPublish()
    {
        var tasks = new Task[ConcurrentInitializers];

        for (int i = 0; i < ConcurrentInitializers; i++)
        {
            var publisherId = i;
            tasks[i] = Task.Run(async () =>
            {
                // First publish to channel triggers MessageStore creation
                await _crossBar.Publish(
                    $"new.channel.{publisherId % 5}",  // 5 channels, lots of contention
                    publisherId,
                    correlationId: 0,
                    key: $"key-{publisherId}",
                    store: true,
                    from: null,
                    tagA: 0);
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests concurrent subscription with state fetching
    /// Forces MessageStore access during initialization
    /// </summary>
    [Benchmark]
    public async Task Channel_ConcurrentSubscribeWithState()
    {
        // Pre-populate some state
        for (int i = 0; i < 10; i++)
        {
            await _crossBar.Publish(
                "initialized.channel",
                i,
                correlationId: 0,
                key: $"key-{i}",
                store: true,
                from: null,
                tagA: 0);
        }

        var tasks = new Task<ISubscription>[ConcurrentInitializers];
        var subscriptions = new List<ISubscription>();

        try
        {
            for (int i = 0; i < ConcurrentInitializers; i++)
            {
                var subId = i;
                tasks[i] = Task.Run(() =>
                {
                    return _crossBar.Subscribe<int>(
                        "initialized.channel",
                        msg => ValueTask.CompletedTask,
                        subscriptionName: $"sub-{subId}",
                        fetchState: true,  // Forces MessageStore access
                        token: default);
                });
            }

            subscriptions.AddRange(await Task.WhenAll(tasks));
        }
        finally
        {
            foreach (var sub in subscriptions)
            {
                sub?.Dispose();
            }
        }
    }
}
