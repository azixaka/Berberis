using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Stateful;

public class StatefulChannelTests
{
    [Fact]
    public async Task Publish_WithKey_StoresMessage()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var msg = TestHelpers.CreateTestMessage("order-data", key: "order-123");

        // Act
        await xBar.Publish("orders", msg, store: true);

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().HaveCount(1);
        state.First().Key.Should().Be("order-123");
        state.First().Body.Should().Be("order-data");
    }

    [Fact]
    public async Task Publish_SameKeyTwice_UpdatesState()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("first-value", key: "order-123"), store: true);
        await Task.Delay(10);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("second-value", key: "order-123"), store: true);

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().HaveCount(1, "same key should update, not add");
        state.First().Key.Should().Be("order-123");
        state.First().Body.Should().Be("second-value", "latest value should overwrite");
    }

    [Fact]
    public async Task Publish_DifferentKeys_StoresMultiple()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("order-1", key: "key-1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("order-2", key: "key-2"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("order-3", key: "key-3"), store: true);

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().HaveCount(3);
        var bodies = state.Select(m => m.Body).ToList();
        bodies.Should().Contain(new[] { "order-1", "order-2", "order-3" });
    }

    [Fact]
    public async Task Publish_WithoutKey_ThrowsException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act & Assert - Publish without key but with store: true should throw
        var act = async () => await xBar.Publish("orders", TestHelpers.CreateTestMessage("order-data", key: null), store: true);

        await act.Should().ThrowAsync<FailedPublishException>()
            .WithMessage("*must have key*");
    }

    [Fact]
    public async Task GetChannelState_ReturnsAllMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-2", key: "k2"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-3", key: "k3"), store: true);

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().HaveCount(3);
    }

    [Fact]
    public void GetChannelState_EmptyChannel_ReturnsEmpty()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act - Create channel but don't store anything
        xBar.Subscribe<string>("empty.channel", _ => ValueTask.CompletedTask, default);

        // Assert
        var state = xBar.GetChannelState<string>("empty.channel");
        state.Should().BeEmpty();
    }

    [Fact]
    public void GetChannelState_NonExistentChannel_ReturnsEmpty()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act & Assert
        var state = xBar.GetChannelState<string>("non.existent.channel");
        state.Should().BeEmpty();
    }

    [Fact]
    public async Task TryDeleteMessage_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("order-data", key: "order-123"), store: true);

        // Act
        var result = xBar.TryDeleteMessage<string>("orders", "order-123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TryDeleteMessage_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var result = xBar.TryDeleteMessage<string>("orders", "non-existent-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryDeleteMessage_RemovedFromState()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-2", key: "k2"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-3", key: "k3"), store: true);

        // Act
        xBar.TryDeleteMessage<string>("orders", "k2");

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().HaveCount(2);
        state.Should().NotContain(m => m.Key == "k2");
        state.Should().Contain(m => m.Key == "k1");
        state.Should().Contain(m => m.Key == "k3");
    }

    [Fact]
    public async Task ResetChannel_ClearsAllState()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-2", key: "k2"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-3", key: "k3"), store: true);

        // Act
        xBar.ResetChannel<string>("orders");

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetChannel_AfterReset_NewStateStored()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("old-msg", key: "old-key"), store: true);

        // Act
        xBar.ResetChannel<string>("orders");
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("new-msg", key: "new-key"), store: true);

        // Assert
        var state = xBar.GetChannelState<string>("orders");
        state.Should().HaveCount(1);
        state.First().Key.Should().Be("new-key");
        state.First().Body.Should().Be("new-msg");
    }

    [Fact]
    public async Task Subscribe_FetchState_ReceivesExistingMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Publish and store some messages
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-2", key: "k2"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("msg-3", key: "k3"), store: true);

        await Task.Delay(50);

        // Act - Subscribe with fetchState: true
        xBar.Subscribe<string>("orders", msg =>
        {
            receivedMessages.Add(msg.Body!);
            if (receivedMessages.Count >= 3)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, fetchState: true, default);

        messageEvent.Wait(TimeSpan.FromSeconds(2));

        // Assert
        receivedMessages.Should().HaveCount(3);
        receivedMessages.Should().Contain("msg-1");
        receivedMessages.Should().Contain("msg-2");
        receivedMessages.Should().Contain("msg-3");
    }

    [Fact]
    public async Task Subscribe_FetchState_ThenNewMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Publish and store some existing messages
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("existing-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("existing-2", key: "k2"), store: true);

        await Task.Delay(50);

        // Subscribe with fetchState: true
        xBar.Subscribe<string>("orders", msg =>
        {
            receivedMessages.Add(msg.Body!);
            if (receivedMessages.Count >= 4)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, fetchState: true, default);

        await Task.Delay(100); // Wait for state to be delivered

        // Act - Publish new messages
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("new-1", key: "k3"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("new-2", key: "k4"), store: true);

        messageEvent.Wait(TimeSpan.FromSeconds(2));

        // Assert
        receivedMessages.Should().HaveCount(4);
        receivedMessages.Should().Contain("existing-1");
        receivedMessages.Should().Contain("existing-2");
        receivedMessages.Should().Contain("new-1");
        receivedMessages.Should().Contain("new-2");
    }

    [Fact]
    public async Task Subscribe_WithoutFetchState_NoHistoricMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Publish and store some messages before subscription
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("old-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("old-2", key: "k2"), store: true);

        await Task.Delay(50);

        // Act - Subscribe without fetchState
        xBar.Subscribe<string>("orders", msg =>
        {
            receivedMessages.Add(msg.Body!);
            messageEvent.Set();
            return ValueTask.CompletedTask;
        }, fetchState: false, default);

        await Task.Delay(100);

        // Publish new message after subscription
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("new-1", key: "k3"), store: true);

        messageEvent.Wait(TimeSpan.FromSeconds(1));

        // Assert
        receivedMessages.Should().ContainSingle();
        receivedMessages.Should().Contain("new-1");
        receivedMessages.Should().NotContain("old-1");
        receivedMessages.Should().NotContain("old-2");
    }

    [Fact]
    public async Task TryGetMessage_ExistingKey_ReturnsMessage()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("order-data", key: "order-123"), store: true);

        // Act
        var found = xBar.TryGetMessage<string>("orders", "order-123", out var message);

        // Assert
        found.Should().BeTrue();
        message.Should().NotBeNull();
        message.Body.Should().Be("order-data");
        message.Key.Should().Be("order-123");
    }

    [Fact]
    public void TryGetMessage_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var found = xBar.TryGetMessage<string>("orders", "non-existent", out var message);

        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public async Task Subscribe_StateDeliveryRaceCondition_NoDuplicates()
    {
        // This test validates the fix for Subscription.cs:120 race condition
        // Scenario: Messages published during state initialization should not be duplicated

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Publish and store state messages
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("state-msg-1", key: "k1"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("state-msg-2", key: "k2"), store: true);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("state-msg-3", key: "k3"), store: true);

        // Act - Subscribe with fetchState and simultaneously publish new messages
        // This simulates the race condition where messages arrive during state initialization
        var subscription = xBar.Subscribe<string>("orders", msg =>
        {
            lock (receivedMessages)
            {
                receivedMessages.Add(msg.Body!);
                if (receivedMessages.Count >= 5)
                    messageEvent.Set();
            }
            return ValueTask.CompletedTask;
        }, fetchState: true, default);

        // Publish messages that might arrive during state delivery
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("concurrent-msg-1", key: "k4"), store: false);
        await xBar.Publish("orders", TestHelpers.CreateTestMessage("concurrent-msg-2", key: "k5"), store: false);

        messageEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        receivedMessages.Should().HaveCount(5, "should receive 3 state messages + 2 new messages");

        // Each message should appear exactly once (no duplicates due to race condition)
        receivedMessages.Should().ContainSingle(m => m == "state-msg-1");
        receivedMessages.Should().ContainSingle(m => m == "state-msg-2");
        receivedMessages.Should().ContainSingle(m => m == "state-msg-3");
        receivedMessages.Should().ContainSingle(m => m == "concurrent-msg-1");
        receivedMessages.Should().ContainSingle(m => m == "concurrent-msg-2");

        // Verify no message appears more than once
        var duplicates = receivedMessages.GroupBy(m => m).Where(g => g.Count() > 1).ToList();
        duplicates.Should().BeEmpty("no messages should be received more than once");
    }
}
