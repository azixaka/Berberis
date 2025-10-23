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
}
