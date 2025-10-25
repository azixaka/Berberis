using System.Diagnostics;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Performance;

/// <summary>
/// Performance benchmarks for MessageStore optimization.
/// These tests establish baseline performance and validate improvements.
/// </summary>
public class MessageStorePerformanceTests
{
    [Fact]
    public async Task MessageStore_HighConcurrencyUpdates_PerformanceAcceptable()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        const int concurrentPublishers = 10;
        const int messagesPerPublisher = 1000;
        const int totalMessages = concurrentPublishers * messagesPerPublisher;

        var receivedCount = 0;
        var manualResetEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>(
            "test.channel",
            msg =>
            {
                if (Interlocked.Increment(ref receivedCount) == totalMessages)
                    manualResetEvent.Set();
                return ValueTask.CompletedTask;
            },
            default);

        // Act - Concurrent stateful updates
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, concurrentPublishers)
            .Select(async publisherId =>
            {
                for (int i = 0; i < messagesPerPublisher; i++)
                {
                    var msg = TestHelpers.CreateTestMessage(
                        $"data-{i}",
                        key: $"key-{publisherId}");
                    await xBar.Publish("test.channel", msg, store: true);
                }
            });

        await Task.WhenAll(tasks);
        manualResetEvent.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        sw.Stop();

        // Assert - Performance benchmarks
        var messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;

        // Output for baseline comparison
        Console.WriteLine($"MessageStore Performance Test:");
        Console.WriteLine($"  Total messages: {totalMessages:N0}");
        Console.WriteLine($"  Duration: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Throughput: {messagesPerSecond:N0} msg/sec");
        Console.WriteLine($"  Avg latency: {sw.Elapsed.TotalMilliseconds / totalMessages:F3}ms per message");

        messagesPerSecond.Should().BeGreaterThan(8_000,
            "MessageStore should handle >8k msg/sec with concurrent updates");

        var state = xBar.GetChannelState<string>("test.channel");
        state.Should().HaveCount(concurrentPublishers,
            "Should have one message per publisher (last update per key)");
    }

    [Fact]
    public async Task MessageStore_MixedReadWrite_NoContention()
    {
        // Test concurrent reads and writes don't cause contention
        var xBar = TestHelpers.CreateTestCrossBar();
        const int duration = 2000; // 2 seconds

        var writeCount = 0;
        var readCount = 0;
        var cts = new CancellationTokenSource(duration);

        // Concurrent writes
        var writeTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var msg = TestHelpers.CreateTestMessage($"data-{writeCount}", key: "test-key");
                await xBar.Publish("test.channel", msg, store: true);
                Interlocked.Increment(ref writeCount);
            }
        });

        // Concurrent reads
        var readTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var state = xBar.GetChannelState<string>("test.channel");
                _ = state.ToList(); // Force enumeration
                Interlocked.Increment(ref readCount);
            }
        });

        await Task.WhenAll(writeTask, readTask);

        // Output for baseline comparison
        Console.WriteLine($"MessageStore Mixed Read/Write Test:");
        Console.WriteLine($"  Duration: {duration}ms");
        Console.WriteLine($"  Writes: {writeCount:N0} ({writeCount / (duration / 1000.0):N0} writes/sec)");
        Console.WriteLine($"  Reads: {readCount:N0} ({readCount / (duration / 1000.0):N0} reads/sec)");

        // Assert - No deadlocks, healthy throughput
        writeCount.Should().BeGreaterThan(1000, "Should achieve >500 writes/sec");
        readCount.Should().BeGreaterThan(1000, "Should achieve >500 reads/sec");
    }

    [Fact]
    public async Task MessageStore_SingleKeyUpdates_MinimalLatency()
    {
        // Test latency for single-threaded updates (no contention scenario)
        var xBar = TestHelpers.CreateTestCrossBar();
        const int iterations = 10000;

        var receivedCount = 0;
        var manualResetEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>(
            "test.channel",
            msg =>
            {
                if (Interlocked.Increment(ref receivedCount) == iterations)
                    manualResetEvent.Set();
                return ValueTask.CompletedTask;
            },
            default);

        // Act - Sequential updates to same key
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"data-{i}", key: "single-key");
            await xBar.Publish("test.channel", msg, store: true);
        }

        manualResetEvent.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        sw.Stop();

        // Assert
        var avgLatencyMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"MessageStore Single-Key Update Test:");
        Console.WriteLine($"  Iterations: {iterations:N0}");
        Console.WriteLine($"  Duration: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Avg latency: {avgLatencyMicroseconds:F2}μs per update");

        // Verify state has only one entry (all updates to same key)
        var state = xBar.GetChannelState<string>("test.channel");
        state.Should().HaveCount(1, "All updates should be to same key");
        state.First().Body.Should().Be($"data-{iterations - 1}", "Should have latest update");
    }

    [Fact]
    public async Task MessageStore_HighContentionUpdates_NoDataCorruption()
    {
        // VALIDATES: Lock-based synchronization prevents corruption
        // SCENARIO: 50 threads, each updating 1000 different keys simultaneously
        // VALIDATES: All 50,000 messages stored correctly, no lost updates

        var xBar = TestHelpers.CreateTestCrossBar();
        const int threadCount = 50;
        const int messagesPerThread = 1000;
        const int totalMessages = threadCount * messagesPerThread;

        var receivedCount = 0;
        var manualResetEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>(
            "test.channel",
            msg =>
            {
                if (Interlocked.Increment(ref receivedCount) == totalMessages)
                    manualResetEvent.Set();
                return ValueTask.CompletedTask;
            },
            default);

        // 50 threads updating concurrently
        var tasks = Enumerable.Range(0, threadCount)
            .Select(async threadId =>
            {
                for (int i = 0; i < messagesPerThread; i++)
                {
                    var msg = TestHelpers.CreateTestMessage(
                        $"thread{threadId}-msg{i}",
                        key: $"thread{threadId}-key{i}");
                    await xBar.Publish("test.channel", msg, store: true);
                }
            });

        await Task.WhenAll(tasks);
        manualResetEvent.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();

        // Assert: All messages stored
        var state = xBar.GetChannelState<string>("test.channel");
        state.Should().HaveCount(totalMessages);

        // Assert: No lost updates (each key present exactly once)
        var keys = state.Select(m => m.Key).ToList();
        keys.Should().OnlyHaveUniqueItems();
        keys.Should().HaveCount(totalMessages);

        Console.WriteLine($"MessageStore High Contention Test:");
        Console.WriteLine($"  Threads: {threadCount}");
        Console.WriteLine($"  Messages per thread: {messagesPerThread}");
        Console.WriteLine($"  Total messages: {totalMessages}");
        Console.WriteLine($"  All messages stored correctly with no corruption");
    }

    [Fact]
    public async Task MessageStore_ConcurrentReadWrite_ConsistentSnapshots()
    {
        // VALIDATES: GetState() returns consistent snapshot during concurrent updates
        // SCENARIO: Thread 1 continuously updates, Thread 2 continuously reads
        // VALIDATES: No exceptions, no partial reads

        var xBar = TestHelpers.CreateTestCrossBar();
        const int durationMs = 3000;

        var writeCount = 0;
        var readCount = 0;
        var inconsistentReads = 0;
        var cts = new CancellationTokenSource(durationMs);

        // Writer thread
        var writeTask = Task.Run(async () =>
        {
            int counter = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                // Update same 100 keys repeatedly
                for (int i = 0; i < 100; i++)
                {
                    var msg = TestHelpers.CreateTestMessage($"value-{counter++}", key: $"key-{i}");
                    await xBar.Publish("test.channel", msg, store: true);
                    Interlocked.Increment(ref writeCount);
                }
            }
        });

        // Reader thread
        var readTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var state = xBar.GetChannelState<string>("test.channel");
                    var list = state.ToList(); // Force enumeration

                    // Verify snapshot consistency (no partial/torn reads)
                    // After initial ramp-up, should always have 100 keys
                    if (list.Count > 0 && list.Count < 100 && writeCount > 200)
                    {
                        Interlocked.Increment(ref inconsistentReads);
                    }

                    Interlocked.Increment(ref readCount);
                }
                catch
                {
                    // Any exception is a failure
                    Interlocked.Increment(ref inconsistentReads);
                }
            }
        });

        await Task.WhenAll(writeTask, readTask);

        // Assert: High throughput achieved
        writeCount.Should().BeGreaterThan(1000);
        readCount.Should().BeGreaterThan(100);

        // Assert: No inconsistent reads
        inconsistentReads.Should().Be(0);

        Console.WriteLine($"MessageStore Concurrent Read/Write Test:");
        Console.WriteLine($"  Duration: {durationMs}ms");
        Console.WriteLine($"  Writes: {writeCount:N0} ({writeCount / (durationMs / 1000.0):N0} writes/sec)");
        Console.WriteLine($"  Reads: {readCount:N0} ({readCount / (durationMs / 1000.0):N0} reads/sec)");
        Console.WriteLine($"  Inconsistent reads: {inconsistentReads} (should be 0)");
    }

    [Fact]
    public async Task MessageStore_LargeState_PerformanceBaseline()
    {
        // VALIDATES: Performance doesn't degrade significantly with large state
        // SCENARIO: Store 100K messages with unique keys
        // ESTABLISHES: Baseline for GetState(), Update(), TryGet() times

        var xBar = TestHelpers.CreateTestCrossBar();
        const int messageCount = 100_000;

        var receivedCount = 0;
        var completionEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<int>(
            "test.channel",
            msg =>
            {
                if (Interlocked.Increment(ref receivedCount) == messageCount)
                    completionEvent.Set();
                return ValueTask.CompletedTask;
            },
            default);

        // Store 100K messages
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < messageCount; i++)
        {
            var msg = TestHelpers.CreateTestMessage(i, key: $"key-{i}");
            await xBar.Publish("test.channel", msg, store: true);
        }

        completionEvent.Wait(TimeSpan.FromMinutes(2)).Should().BeTrue();
        sw.Stop();

        var storeTime = sw.Elapsed;

        // Benchmark GetState()
        sw.Restart();
        var state = xBar.GetChannelState<int>("test.channel");
        var stateList = state.ToList();
        sw.Stop();

        var getStateTime = sw.Elapsed;

        // Assert: Performance acceptable
        stateList.Should().HaveCount(messageCount);
        getStateTime.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "GetState() should complete within 1 second for 100K entries");

        Console.WriteLine($"MessageStore Large State Performance Test:");
        Console.WriteLine($"  Message count: {messageCount:N0}");
        Console.WriteLine($"  Store time: {storeTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  GetState() time: {getStateTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  Throughput: {messageCount / storeTime.TotalSeconds:N0} msg/sec");
    }
}
