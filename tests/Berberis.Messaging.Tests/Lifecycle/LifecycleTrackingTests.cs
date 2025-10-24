using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace Berberis.Messaging.Tests.Lifecycle;

public class LifecycleTrackingTests
{
    [Fact]
    public async Task ChannelCreated_PublishesLifecycleEvent()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();
        var sub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act
        await xBar.Publish("test.channel", "test message");
        await Task.Delay(100); // Give time for event to be processed

        // Assert
        events.Should().ContainSingle(e =>
            e.EventType == LifecycleEventType.ChannelCreated &&
            e.ChannelName == "test.channel" &&
            e.MessageBodyType.Contains("String"));
    }

    [Fact]
    public async Task SubscriptionCreated_PublishesLifecycleEvent()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act
        var testSub = xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, "TestSubscription", CancellationToken.None);

        await Task.Delay(100); // Give time for event to be processed

        // Assert
        events.Should().ContainSingle(e =>
            e.EventType == LifecycleEventType.SubscriptionCreated &&
            e.ChannelName == "test.channel" &&
            e.SubscriptionName.Contains("TestSubscription"));
    }

    [Fact]
    public async Task SubscriptionDisposed_PublishesLifecycleEvent()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        var testSub = xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, "TestSubscription", CancellationToken.None);

        await Task.Delay(100); // Give time for creation event

        // Act
        testSub.Dispose();
        await Task.Delay(100); // Give time for disposal event

        // Assert
        events.Should().Contain(e =>
            e.EventType == LifecycleEventType.SubscriptionDisposed &&
            e.ChannelName == "test.channel" &&
            e.SubscriptionName.Contains("TestSubscription"));
    }

    [Fact]
    public async Task ChannelDeleted_PublishesLifecycleEvent()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        await xBar.Publish("test.channel", "test message");
        await Task.Delay(100); // Give time for channel creation

        // Act
        xBar.TryDeleteChannel("test.channel");
        await Task.Delay(100); // Give time for deletion event

        // Assert
        events.Should().Contain(e =>
            e.EventType == LifecycleEventType.ChannelDeleted &&
            e.ChannelName == "test.channel");
    }

    [Fact]
    public async Task LifecycleChannel_DoesNotPublishEventForItself()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();

        // Act - subscribe to lifecycle channel (this creates it)
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        await Task.Delay(100);

        // Assert - should not have event for $lifecycle channel creation
        events.Should().NotContain(e => e.ChannelName == "$lifecycle");
    }

    [Fact]
    public async Task SystemChannelSubscription_DoesNotPublishLifecycleEvent()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true, EnableMessageTracing = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        await Task.Delay(100);

        // Act - subscribe to system tracing channel
        var tracingSub = xBar.Subscribe<MessageTrace>("$message.traces", _ => ValueTask.CompletedTask, CancellationToken.None);
        await Task.Delay(100);

        // Assert - should not have event for system channel subscription
        events.Should().NotContain(e => e.ChannelName == "$message.traces");
    }

    [Fact]
    public async Task WildcardSubscription_PublishesLifecycleEvent()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        var events = new ConcurrentBag<LifecycleEvent>();
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - create wildcard subscription
        var wildcardSub = xBar.Subscribe<string>("test.*", _ => ValueTask.CompletedTask, "WildcardSub", CancellationToken.None);

        await Task.Delay(100);

        // Assert
        events.Should().ContainSingle(e =>
            e.EventType == LifecycleEventType.SubscriptionCreated &&
            e.ChannelName == "test.*" &&
            e.SubscriptionName.Contains("WildcardSub"));
    }

    [Fact]
    public async Task TrackingDisabled_DoesNotPublishEvents()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = false };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        // Manually enable lifecycle channel for subscription
        xBar.LifecycleTrackingEnabled = false;

        var events = new ConcurrentBag<LifecycleEvent>();
        // This won't work without enabling tracking first, so enable it temporarily
        xBar.LifecycleTrackingEnabled = true;
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            events.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);
        xBar.LifecycleTrackingEnabled = false; // Disable again

        // Act
        await xBar.Publish("test.channel", "test message");
        await Task.Delay(100);

        // Assert - no events should be published when tracking is disabled
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task LifecycleEvent_ContainsCorrectTimestamp()
    {
        // Arrange
        var options = new CrossBarOptions { EnableLifecycleTracking = true };
        var xBar = new CrossBar(NullLoggerFactory.Instance, options);

        LifecycleEvent? capturedEvent = null;
        var lifecycleSub = xBar.Subscribe<LifecycleEvent>("$lifecycle", msg =>
        {
            capturedEvent = msg.Body;
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        var before = DateTime.UtcNow;

        // Act
        await xBar.Publish("test.channel", "test message");
        await Task.Delay(100);

        var after = DateTime.UtcNow;

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Value.Timestamp.Should().BeAfter(before.AddSeconds(-1));
        capturedEvent!.Value.Timestamp.Should().BeBefore(after.AddSeconds(1));
    }

    [Fact]
    public void LifecycleEvent_ToString_FormatsCorrectly()
    {
        // Arrange
        var lifecycleEvent = new LifecycleEvent
        {
            EventType = LifecycleEventType.SubscriptionCreated,
            ChannelName = "test.channel",
            SubscriptionName = "TestSub-[1]",
            MessageBodyType = "System.String",
            Timestamp = new DateTime(2025, 10, 24, 10, 30, 45, DateTimeKind.Utc)
        };

        // Act
        var result = lifecycleEvent.ToString();

        // Assert
        result.Should().Contain("SubscriptionCreated");
        result.Should().Contain("test.channel");
        result.Should().Contain("TestSub-[1]");
        result.Should().Contain("System.String");
    }
}
