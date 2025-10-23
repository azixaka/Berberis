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

        messagesPerSecond.Should().BeGreaterThan(10_000,
            "MessageStore should handle >10k msg/sec with concurrent updates");

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
}
