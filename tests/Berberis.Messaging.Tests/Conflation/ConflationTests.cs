using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Conflation;

public class ConflationTests
{
    [Fact]
    public async Task Conflation_UpdatesSameKey_OnlyLatestReceived()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveLock = new object();

        xBar.Subscribe<string>(
            "prices",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(500),
            default);  // cancellationToken

        // Act - Publish multiple updates to same key rapidly
        for (int i = 0; i < 100; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"price-{i}", key: "AAPL");
            await xBar.Publish("prices", msg, store: false);
            await Task.Delay(5);
        }

        // Wait for conflation to flush
        await Task.Delay(1000);

        // Assert - Should receive far fewer than 100 messages due to conflation
        received.Should().HaveCountLessThan(100);
        received.Should().HaveCountGreaterThan(0);

        // Latest value should be present
        received.Last().Body.Should().Contain("price-99");
    }

    [Fact]
    public async Task Conflation_DifferentKeys_AllReceived()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveLock = new object();
        var messageCount = 10;

        xBar.Subscribe<string>(
            "prices",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(500),
            default);  // cancellationToken

        // Act - Publish updates to different keys
        for (int i = 0; i < messageCount; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"price-{i}", key: $"STOCK-{i}");
            await xBar.Publish("prices", msg, store: false);
            await Task.Delay(10);
        }

        // Wait for conflation to flush
        await Task.Delay(1000);

        // Assert - All messages with different keys should be received
        received.Should().HaveCount(messageCount);
        received.Select(m => m.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Conflation_MessagesWithoutKey_ProcessedImmediately()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveLock = new object();

        xBar.Subscribe<string>(
            "channel",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(500),
            default);  // cancellationToken

        // Act - Publish messages without keys
        for (int i = 0; i < 10; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"msg-{i}", key: null);
            await xBar.Publish("channel", msg, store: false);
        }

        // Small delay to allow immediate processing
        await Task.Delay(100);

        // Assert - All messages without keys should be processed immediately, not conflated
        received.Should().HaveCount(10);
    }

    [Fact]
    public async Task Conflation_FlushInterval_BatchesMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveTimes = new List<DateTime>();
        var receiveLock = new object();
        var flushInterval = TimeSpan.FromMilliseconds(300);

        xBar.Subscribe<string>(
            "channel",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                    receiveTimes.Add(DateTime.UtcNow);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            flushInterval,
            default);  // cancellationToken

        // Act - Publish messages continuously with same key
        var publishStart = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"value-{i}", key: "KEY1");
            await xBar.Publish("channel", msg, store: false);
            await Task.Delay(10);
        }

        // Wait for final flush
        await Task.Delay(1000);

        // Assert - Messages should be batched, not all 50 received
        received.Should().HaveCountLessThan(50);
        received.Should().HaveCountGreaterThan(0);

        // Verify batching by checking receive times
        if (receiveTimes.Count > 1)
        {
            var timeDifferences = new List<double>();
            for (int i = 1; i < receiveTimes.Count; i++)
            {
                timeDifferences.Add((receiveTimes[i] - receiveTimes[i - 1]).TotalMilliseconds);
            }

            // At least some time differences should be close to flush interval
            timeDifferences.Should().Contain(diff => diff >= flushInterval.TotalMilliseconds * 0.8);
        }
    }

    [Fact]
    public async Task Conflation_NoInterval_NoConflation()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveLock = new object();

        xBar.Subscribe<string>(
            "channel",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.Zero,
            default);  // cancellationToken // Conflation disabled

        // Act - Publish multiple updates to same key
        for (int i = 0; i < 20; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"value-{i}", key: "KEY1");
            await xBar.Publish("channel", msg, store: false);
        }

        // Wait for processing
        await Task.Delay(200);

        // Assert - All 20 messages should be received (no conflation)
        received.Should().HaveCount(20);
    }

    [Fact]
    public async Task Conflation_MultipleKeys_EachConflatedIndependently()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveLock = new object();

        xBar.Subscribe<string>(
            "prices",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(400),
            default);  // cancellationToken

        // Act - Publish updates to 3 different keys, multiple updates per key
        var keys = new[] { "AAPL", "GOOGL", "MSFT" };
        for (int i = 0; i < 30; i++)
        {
            var key = keys[i % 3];
            var msg = TestHelpers.CreateTestMessage($"{key}-price-{i}", key: key);
            await xBar.Publish("prices", msg, store: false);
            await Task.Delay(5);
        }

        // Wait for conflation
        await Task.Delay(1000);

        // Assert - Should receive messages for all 3 keys
        var uniqueKeys = received.Select(m => m.Key).Distinct().ToList();
        uniqueKeys.Should().HaveCount(3);
        uniqueKeys.Should().Contain("AAPL");
        uniqueKeys.Should().Contain("GOOGL");
        uniqueKeys.Should().Contain("MSFT");

        // Each key should have fewer than the full 10 messages it received
        received.Where(m => m.Key == "AAPL").Should().HaveCountLessThan(10);
        received.Where(m => m.Key == "GOOGL").Should().HaveCountLessThan(10);
        received.Where(m => m.Key == "MSFT").Should().HaveCountLessThan(10);
    }

    [Fact]
    public async Task Conflation_HighThroughput_ReducesMessageCount()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var publishCount = 500;

        xBar.Subscribe<string>(
            "channel",
            msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(200),
            default);  // cancellationToken

        // Act - High throughput publishing with same key
        var publishTasks = Enumerable.Range(0, publishCount).Select(async i =>
        {
            var msg = TestHelpers.CreateTestMessage($"value-{i}", key: "HIGH_FREQ");
            await xBar.Publish("channel", msg, store: false);
        });

        await Task.WhenAll(publishTasks);

        // Wait for conflation
        await Task.Delay(1500);

        // Assert - Conflation should significantly reduce message count
        receivedCount.Should().BeLessThan(publishCount);
        receivedCount.Should().BeGreaterThan(0);

        // Should reduce to less than 20% of original messages
        receivedCount.Should().BeLessThan(publishCount / 5);
    }

    [Fact]
    public async Task Conflation_MixedKeysAndNoKeys_HandledCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();
        var receiveLock = new object();

        xBar.Subscribe<string>(
            "channel",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(300),
            default);  // cancellationToken

        // Act - Mix messages with and without keys
        for (int i = 0; i < 20; i++)
        {
            if (i % 2 == 0)
            {
                // Messages with key (should be conflated)
                var msg = TestHelpers.CreateTestMessage($"keyed-{i}", key: "KEY1");
                await xBar.Publish("channel", msg, store: false);
            }
            else
            {
                // Messages without key (should not be conflated)
                var msg = TestHelpers.CreateTestMessage($"nokey-{i}", key: null);
                await xBar.Publish("channel", msg, store: false);
            }
            await Task.Delay(10);
        }

        // Wait for processing
        await Task.Delay(800);

        // Assert
        // All 10 no-key messages should be received
        received.Where(m => string.IsNullOrEmpty(m.Key)).Should().HaveCount(10);

        // Keyed messages should be conflated (less than 10)
        received.Where(m => !string.IsNullOrEmpty(m.Key)).Should().HaveCountLessThan(10);
    }

    [Fact]
    public async Task Conflation_SubscriptionDisposal_StopsFlushLoop()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;

        var subscription = xBar.Subscribe<string>(
            "channel",
            msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(200),
            default);  // cancellationToken

        // Act - Publish some messages and wait for initial conflation flush
        for (int i = 0; i < 10; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"value-{i}", key: "KEY1");
            await xBar.Publish("channel", msg, store: false);
        }

        // Wait for at least one flush cycle to complete
        await Task.Delay(500);
        var countBeforeDispose = receivedCount;

        // Dispose subscription
        subscription.Dispose();
        await Task.Delay(300);

        // Publish more messages after disposal
        for (int i = 10; i < 20; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"value-{i}", key: "KEY1");
            await xBar.Publish("channel", msg, store: false);
        }

        // Wait to ensure no messages are processed
        await Task.Delay(500);

        // Assert - No new messages should be received after disposal
        receivedCount.Should().Be(countBeforeDispose);
    }

    [Fact]
    public async Task Conflation_LatestValuePreserved_WhenMultipleUpdates()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<int>>();
        var receiveLock = new object();

        xBar.Subscribe<int>(
            "channel",
            msg =>
            {
                lock (receiveLock)
                {
                    received.Add(msg);
                }
                return ValueTask.CompletedTask;
            },
            "",  // subscriptionName
            false,  // fetchState
            TimeSpan.FromMilliseconds(400),
            default);  // cancellationToken

        // Act - Publish incrementing values with same key
        for (int i = 0; i < 50; i++)
        {
            var msg = TestHelpers.CreateTestMessage(i, key: "COUNTER");
            await xBar.Publish("channel", msg, store: false);
            await Task.Delay(5);
        }

        // Wait for final flush
        await Task.Delay(1000);

        // Assert - Should have received conflated messages
        received.Should().HaveCountLessThan(50);

        // The last received message should be the latest value (49)
        received.Last().Body.Should().Be(49);
    }
}
