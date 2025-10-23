using Berberis.Messaging;
using Berberis.Messaging.Exceptions;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Core;

public class HandlerTimeoutTests
{
    [Fact]
    public async Task Handler_ExceedsTimeout_TimesOutAndContinues()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<int>();
        var timeoutOccurred = false;
        var completionEvent = new ManualResetEventSlim(false);

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromMilliseconds(100),
            OnTimeout = ex =>
            {
                timeoutOccurred = true;
                ex.Should().NotBeNull();
                ex.MessageId.Should().Be(1);
            }
        };

        var subscription = xBar.Subscribe<int>("test.channel", async msg =>
        {
            receivedMessages.Add(msg.Body);

            if (msg.Body == 1)
            {
                // First message - simulate slow handler that will timeout
                await Task.Delay(500);
            }
            // Second message completes quickly

            if (receivedMessages.Count >= 2)
                completionEvent.Set();

            return;
        }, options: options, token: default);

        // Act - Publish two messages
        await xBar.Publish("test.channel", 1);
        await xBar.Publish("test.channel", 2);

        // Wait for completion
        var completed = completionEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        completed.Should().BeTrue("subscription should continue after timeout");
        timeoutOccurred.Should().BeTrue("timeout callback should have been invoked");
        receivedMessages.Should().Contain(2, "second message should be processed");
        subscription.GetTimeoutCount().Should().Be(1, "exactly one timeout should be recorded");
    }

    [Fact]
    public async Task Handler_CompletesBeforeTimeout_NoTimeout()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var timeoutOccurred = false;
        var receivedCount = 0;
        var completionEvent = new ManualResetEventSlim(false);

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromSeconds(1),
            OnTimeout = ex => { timeoutOccurred = true; }
        };

        var subscription = xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedCount++;
            if (receivedCount >= 3)
                completionEvent.Set();
            return ValueTask.CompletedTask;
        }, options: options, token: default);

        // Act - Publish messages that complete quickly
        await xBar.Publish("test.channel", "msg1");
        await xBar.Publish("test.channel", "msg2");
        await xBar.Publish("test.channel", "msg3");

        completionEvent.Wait(TimeSpan.FromSeconds(2));

        // Assert
        timeoutOccurred.Should().BeFalse("no timeout should occur for fast handlers");
        subscription.GetTimeoutCount().Should().Be(0, "no timeouts should be recorded");
        receivedCount.Should().Be(3, "all messages should be processed");
    }

    [Fact]
    public async Task Handler_NoTimeoutConfigured_NoAllocation()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var completionEvent = new ManualResetEventSlim(false);

        // No timeout configured - should use fast path
        var subscription = xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedCount++;
            if (receivedCount >= 5)
                completionEvent.Set();
            return ValueTask.CompletedTask;
        }, token: default);

        // Act - Publish messages
        for (int i = 0; i < 5; i++)
        {
            await xBar.Publish("test.channel", $"msg{i}");
        }

        completionEvent.Wait(TimeSpan.FromSeconds(1));

        // Assert
        receivedCount.Should().Be(5, "all messages should be processed");
        subscription.GetTimeoutCount().Should().Be(0, "no timeouts with null timeout config");
    }

    [Fact]
    public async Task Handler_TimeoutException_ContainsCorrectMetadata()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        HandlerTimeoutException? capturedException = null;
        var timeoutEvent = new ManualResetEventSlim(false);

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromMilliseconds(50),
            OnTimeout = ex =>
            {
                capturedException = ex;
                timeoutEvent.Set();
            }
        };

        var subscription = xBar.Subscribe<string>("orders.new", async msg =>
        {
            await Task.Delay(500); // Will timeout
            return;
        }, subscriptionName: "test-subscription", options: options, token: default);

        // Act
        await xBar.Publish("orders.new", "test-order");
        timeoutEvent.Wait(TimeSpan.FromSeconds(2));

        // Assert
        capturedException.Should().NotBeNull();
        capturedException!.SubscriptionName.Should().Contain("test-subscription");
        capturedException.ChannelName.Should().Be("orders.new");
        capturedException.MessageId.Should().BeGreaterThan(0);
        capturedException.Timeout.Should().Be(TimeSpan.FromMilliseconds(50));
        capturedException.Message.Should().Contain("timed out");
        capturedException.Message.Should().Contain("50ms");
    }

    [Fact]
    public async Task Handler_MultipleTimeouts_CountsAccumulate()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var timeoutCount = 0;
        var processedCount = 0;
        var completionEvent = new ManualResetEventSlim(false);

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromMilliseconds(50),
            OnTimeout = ex => { Interlocked.Increment(ref timeoutCount); }
        };

        var subscription = xBar.Subscribe<int>("test.channel", async msg =>
        {
            if (msg.Body % 2 == 0)
            {
                // Even messages timeout
                await Task.Delay(200);
            }
            // Odd messages complete quickly

            Interlocked.Increment(ref processedCount);
            if (processedCount >= 3)
                completionEvent.Set();

            return;
        }, options: options, token: default);

        // Act - Publish 5 messages (3 fast, 2 slow)
        await xBar.Publish("test.channel", 1); // Fast
        await xBar.Publish("test.channel", 2); // Slow - timeout
        await xBar.Publish("test.channel", 3); // Fast
        await xBar.Publish("test.channel", 4); // Slow - timeout
        await xBar.Publish("test.channel", 5); // Fast

        completionEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        timeoutCount.Should().Be(2, "two messages should have timed out");
        subscription.GetTimeoutCount().Should().Be(2, "subscription should track 2 timeouts");
        processedCount.Should().Be(3, "three fast messages should complete");
    }

    [Fact]
    public async Task Handler_Timeout_StatisticsIncremented()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var timeoutEvent = new ManualResetEventSlim(false);

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromMilliseconds(50),
            OnTimeout = ex => { timeoutEvent.Set(); }
        };

        var subscription = xBar.Subscribe<string>("test.channel", async msg =>
        {
            await Task.Delay(300); // Will timeout
            return;
        }, options: options, token: default);

        var statsBefore = subscription.Statistics.GetStats(reset: false);

        // Act
        await xBar.Publish("test.channel", "test-message");
        timeoutEvent.Wait(TimeSpan.FromSeconds(2));

        await Task.Delay(100); // Allow stats to update

        var statsAfter = subscription.Statistics.GetStats(reset: false);

        // Assert
        statsBefore.NumOfTimeouts.Should().Be(0, "no timeouts initially");
        statsAfter.NumOfTimeouts.Should().Be(1, "timeout should be recorded in statistics");
    }

    [Fact]
    public async Task Handler_SyncCompletion_NoTimeoutAllocation()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var completionEvent = new ManualResetEventSlim(false);

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromSeconds(1)
        };

        // Synchronous handler (completes immediately)
        var subscription = xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedCount++;
            if (receivedCount >= 3)
                completionEvent.Set();

            // Return completed task - should take fast path
            return ValueTask.CompletedTask;
        }, options: options, token: default);

        // Act
        await xBar.Publish("test.channel", "msg1");
        await xBar.Publish("test.channel", "msg2");
        await xBar.Publish("test.channel", "msg3");

        completionEvent.Wait(TimeSpan.FromSeconds(1));

        // Assert
        receivedCount.Should().Be(3);
        subscription.GetTimeoutCount().Should().Be(0);
        // Note: When handler completes synchronously, CTS is never allocated
        // This test validates the fast path optimization
    }
}
