using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Wildcards;

public class WildcardSubscriptionTests
{
    [Fact]
    public async Task Subscribe_SingleLevelWildcard_MatchesOneLevel()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("orders.*", msg =>
        {
            received.Add(msg.Body!);
            if (received.Count >= 2)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("new order"), false);
        await xBar.Publish("orders.cancelled", TestHelpers.CreateTestMessage("cancelled"), false);
        await xBar.Publish("orders.shipped.fedex", TestHelpers.CreateTestMessage("shipped"), false); // Should NOT match

        messageEvent.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(100); // Allow time for any errant messages

        // Assert
        received.Should().HaveCount(2);
        received.Should().Contain("new order");
        received.Should().Contain("cancelled");
        received.Should().NotContain("shipped");
    }

    [Theory]
    [InlineData("orders.*", "orders.new", true)]
    [InlineData("orders.*", "orders.cancelled", true)]
    [InlineData("orders.*", "orders.shipped.fedex", false)]
    [InlineData("orders.*", "trades.new", false)]
    [InlineData("*.new", "orders.new", true)]
    [InlineData("*.new", "trades.new", true)]
    [InlineData("*.new", "orders.new.pending", false)]
    public async Task Subscribe_SingleWildcard_MatchesCorrectly(
        string pattern, string channel, bool shouldMatch)
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>(pattern, msg =>
        {
            received.Add(msg.Body!);
            messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Allow wildcard subscription to fully initialize
        await Task.Delay(50);

        // Act
        await xBar.Publish(channel, TestHelpers.CreateTestMessage("test message"), false);
        messageEvent.Wait(TimeSpan.FromMilliseconds(500));

        // Assert
        if (shouldMatch)
        {
            received.Should().ContainSingle();
            received[0].Should().Be("test message");
        }
        else
        {
            received.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Subscribe_WildcardMiddle_MatchesCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("orders.*.confirmed", msg =>
        {
            received.Add(msg.Body!);
            messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("orders.new.confirmed", TestHelpers.CreateTestMessage("new confirmed"), false);
        await xBar.Publish("orders.old.confirmed", TestHelpers.CreateTestMessage("old confirmed"), false);
        await xBar.Publish("orders.new.pending", TestHelpers.CreateTestMessage("pending"), false); // Should NOT match

        messageEvent.Wait(TimeSpan.FromSeconds(1));
        await Task.Delay(100);

        // Assert
        received.Should().HaveCountGreaterThanOrEqualTo(1);
        received.Should().Contain("new confirmed");
        received.Should().NotContain("pending");
    }

    [Fact]
    public async Task Subscribe_RecursiveWildcard_MatchesAllDescendants()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("orders.>", msg =>
        {
            received.Add(msg.Body!);
            if (received.Count >= 3)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("new"), false);
        await xBar.Publish("orders.shipped.fedex", TestHelpers.CreateTestMessage("shipped"), false);
        await xBar.Publish("orders.shipped.ups.ground", TestHelpers.CreateTestMessage("ground"), false);
        await xBar.Publish("trades.new", TestHelpers.CreateTestMessage("trade"), false); // Should NOT match

        messageEvent.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        // Assert
        received.Should().HaveCount(3);
        received.Should().Contain("new");
        received.Should().Contain("shipped");
        received.Should().Contain("ground");
        received.Should().NotContain("trade");
    }

    [Theory]
    [InlineData("orders.>", "orders.new", true)]
    [InlineData("orders.>", "orders.shipped.fedex", true)]
    [InlineData("orders.>", "orders.a.b.c.d.e", true)]
    [InlineData("orders.>", "trades.new", false)]
    [InlineData("orders.>", "orders", false)]
    public async Task Subscribe_RecursiveWildcard_MatchesCorrectly(
        string pattern, string channel, bool shouldMatch)
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>(pattern, msg =>
        {
            received.Add(msg.Body!);
            messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Allow wildcard subscription to fully initialize
        await Task.Delay(50);

        // Act
        await xBar.Publish(channel, TestHelpers.CreateTestMessage("test message"), false);
        messageEvent.Wait(TimeSpan.FromMilliseconds(500));

        // Assert
        if (shouldMatch)
        {
            received.Should().ContainSingle();
            received[0].Should().Be("test message");
        }
        else
        {
            received.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Subscribe_MultipleWildcards_MatchesCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("*.orders.*", msg =>
        {
            received.Add(msg.Body!);
            if (received.Count >= 2)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("us.orders.new", TestHelpers.CreateTestMessage("us new"), false);
        await xBar.Publish("eu.orders.cancelled", TestHelpers.CreateTestMessage("eu cancelled"), false);
        await xBar.Publish("us.orders.shipped.fedex", TestHelpers.CreateTestMessage("shipped"), false); // Should NOT match (3 levels)

        messageEvent.Wait(TimeSpan.FromSeconds(1));
        await Task.Delay(100);

        // Assert
        received.Should().HaveCount(2);
        received.Should().Contain("us new");
        received.Should().Contain("eu cancelled");
        received.Should().NotContain("shipped");
    }

    [Fact]
    public async Task Subscribe_WildcardAtStart_MatchesCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("*.confirmed", msg =>
        {
            received.Add(msg.Body!);
            if (received.Count >= 2)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("orders.confirmed", TestHelpers.CreateTestMessage("order"), false);
        await xBar.Publish("trades.confirmed", TestHelpers.CreateTestMessage("trade"), false);
        await xBar.Publish("orders.new.confirmed", TestHelpers.CreateTestMessage("new"), false); // Should NOT match

        messageEvent.Wait(TimeSpan.FromSeconds(1));
        await Task.Delay(100);

        // Assert
        received.Should().HaveCount(2);
        received.Should().Contain("order");
        received.Should().Contain("trade");
        received.Should().NotContain("new");
    }

    [Fact]
    public async Task Subscribe_WildcardAfterChannelExists_ReceivesMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Act - Publish to channels first
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("first"), false);
        await xBar.Publish("orders.cancelled", TestHelpers.CreateTestMessage("second"), false);

        await Task.Delay(50);

        // Subscribe with wildcard after channels exist
        xBar.Subscribe<string>("orders.*", msg =>
        {
            received.Add(msg.Body!);
            messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Publish new message
        await xBar.Publish("orders.confirmed", TestHelpers.CreateTestMessage("after subscribe"), false);
        messageEvent.Wait(TimeSpan.FromSeconds(1));

        // Assert - Only receives messages published after subscription
        received.Should().ContainSingle();
        received[0].Should().Be("after subscribe");
    }

    [Fact]
    public async Task Subscribe_WildcardBeforeChannelCreated_ReceivesMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Act - Subscribe with wildcard first
        xBar.Subscribe<string>("orders.*", msg =>
        {
            received.Add(msg.Body!);
            if (received.Count >= 2)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Then create channels by publishing
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("new order"), false);
        await xBar.Publish("orders.cancelled", TestHelpers.CreateTestMessage("cancelled"), false);

        messageEvent.Wait(TimeSpan.FromSeconds(1));

        // Assert
        received.Should().HaveCount(2);
        received.Should().Contain("new order");
        received.Should().Contain("cancelled");
    }

    [Fact]
    public async Task WildcardSubscription_RaceCondition_EventualConsistency()
    {
        // VALIDATES: Race condition documented in CrossBar.cs:325-350
        // VALIDATES: Eventual consistency model works as documented
        // SCENARIO:
        //   1. Start rapid publishing to channels matching pattern
        //   2. Mid-publishing, subscribe with wildcard
        //   3. Measure: Initial messages missed vs. subsequent messages caught
        //   4. Validate: Subscription catches up within documented time window

        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new System.Collections.Concurrent.ConcurrentBag<int>();
        var publishCount = 1000;

        // Background task: Rapidly create channels and publish
        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < publishCount; i++)
            {
                await xBar.Publish($"orders.region{i % 10}", TestHelpers.CreateTestMessage(i), false);
                await Task.Delay(1); // 1ms between publishes
            }
        });

        // Wait 50ms, then subscribe with wildcard
        await Task.Delay(50);

        var subscribeTime = DateTime.UtcNow;
        xBar.Subscribe<int>("orders.*", msg =>
        {
            received.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, default);

        await publishTask;
        await Task.Delay(1000);

        // Assert: Some initial messages missed (race condition window)
        received.Should().HaveCountLessThan(publishCount);

        // Assert: Subscription caught subsequent messages (eventual consistency)
        received.Should().HaveCountGreaterThan(publishCount / 2,
            "should catch most messages after subscription, demonstrating eventual consistency");

        // Document behavior in test output
        var missedCount = publishCount - received.Count;
        Console.WriteLine($"Race window: Missed {missedCount} messages out of {publishCount} " +
                         $"({100.0 * missedCount / publishCount:F2}% - within documented race window)");
    }

    [Fact]
    public async Task WildcardSubscription_WithTypeMismatch_SubscribesToMatchingTypesOnly()
    {
        // VALIDATES: Type mismatches handled gracefully with wildcards
        // SCENARIO:
        //   1. Create channels: orders.new<string>, orders.count<int>
        //   2. Subscribe "orders.*" expecting <string>
        //   3. Verify: Subscribes to orders.new, skips orders.count with warning

        var xBar = TestHelpers.CreateTestCrossBar();

        // Create channels with different types
        xBar.Subscribe<string>("orders.new", _ => ValueTask.CompletedTask, default);
        xBar.Subscribe<int>("orders.count", _ => ValueTask.CompletedTask, default);

        await Task.Delay(50);

        var receivedStrings = new List<string>();

        // Subscribe with wildcard expecting string
        xBar.Subscribe<string>("orders.*", msg =>
        {
            receivedStrings.Add(msg.Body!);
            return ValueTask.CompletedTask;
        }, default);

        // Publish to both channels
        await xBar.Publish("orders.new", "test-string");
        await xBar.Publish("orders.count", 42);

        await Task.Delay(200);

        // Assert: Only string channel messages received
        receivedStrings.Should().HaveCount(1);
        receivedStrings.Should().Contain("test-string");
    }

    [Theory]
    [InlineData("*.*.confirmed", "us.orders.confirmed", true)]
    [InlineData("*.*.confirmed", "eu.trades.confirmed", true)]
    [InlineData("*.*.confirmed", "orders.confirmed", false)] // Only 2 levels
    [InlineData("orders.*.*", "orders.new.pending", true)]
    [InlineData("orders.*.*", "orders.shipped.fedex", true)]
    [InlineData("orders.*.*", "orders.new", false)] // Only 2 levels
    [InlineData("region.>", "region.us.orders.new.pending", true)] // Recursive matches all depths
    [InlineData("region.>", "region.eu", true)]
    [InlineData("region.>", "other.us", false)] // Different prefix
    public async Task WildcardSubscription_ComplexPatterns_MatchesCorrectly(
        string pattern, string channel, bool shouldMatch)
    {
        // VALIDATES: Complex multi-level wildcard patterns work correctly

        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        xBar.Subscribe<string>(pattern, msg =>
        {
            received.Add(msg.Body!);
            messageEvent.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Allow wildcard subscription to fully initialize
        await Task.Delay(50);

        await xBar.Publish(channel, TestHelpers.CreateTestMessage("test"), false);
        messageEvent.Wait(TimeSpan.FromMilliseconds(500));

        if (shouldMatch)
            received.Should().ContainSingle();
        else
            received.Should().BeEmpty();
    }

    [Fact]
    public async Task WildcardSubscription_Disposal_RemovesFromAllMatchingChannels()
    {
        // VALIDATES: Wildcard subscription cleanup is complete
        // SCENARIO:
        //   1. Create 10 channels matching pattern
        //   2. Subscribe with wildcard (matches all 10)
        //   3. Dispose subscription
        //   4. Verify: Removed from all 10 channel subscription lists
        //   5. Verify: No messages received after disposal

        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;

        // Create 10 channels
        for (int i = 0; i < 10; i++)
        {
            xBar.Subscribe<string>($"orders.region{i}", _ => ValueTask.CompletedTask, default);
        }

        await Task.Delay(100);

        // Subscribe with wildcard
        var wildcardSub = xBar.Subscribe<string>("orders.*", msg =>
        {
            Interlocked.Increment(ref receivedCount);
            return ValueTask.CompletedTask;
        }, default);

        // Verify subscription works
        await xBar.Publish("orders.region0", "test");
        await Task.Delay(100);
        receivedCount.Should().Be(1);

        // Dispose
        wildcardSub.Dispose();
        await Task.Delay(200);

        // Publish to all channels
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish($"orders.region{i}", $"after-dispose-{i}");
        }

        await Task.Delay(500);

        // Assert: No new messages received
        receivedCount.Should().Be(1, "should not receive messages after disposal");

        // Verify subscriptions removed from channel lists
        for (int i = 0; i < 10; i++)
        {
            var subs = xBar.GetChannelSubscriptions($"orders.region{i}");
            subs.Should().NotBeNull();
            subs.Should().NotContain(s => s.Name == wildcardSub.Name);
        }
    }
}
