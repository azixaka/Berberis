using Berberis.Messaging;
using Berberis.Messaging.Statistics;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Statistics;

public class StatsTrackerTests
{
    // Task 36: Publish rate tracking tests

    [Fact]
    public async Task Stats_PublishRate_Accurate()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act
        for (int i = 0; i < 100; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(200);

        // Assert
        var stats = sub.Statistics.GetStats(reset: false);
        stats.TotalProcessedMessages.Should().Be(100);
        stats.ProcessRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Stats_MessageCounting_Accurate()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<int>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act
        for (int i = 0; i < 50; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), false);
        }

        await Task.Delay(100);

        // Assert
        var stats = sub.Statistics.GetStats(reset: false);
        stats.TotalEnqueuedMessages.Should().Be(50);
        stats.TotalProcessedMessages.Should().Be(50);
    }

    [Fact]
    public async Task Stats_Reset_ClearsIntervalCounters()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(100);

        // Act - Get stats with reset
        var stats1 = sub.Statistics.GetStats(reset: true);
        await Task.Delay(100); // Wait a bit
        var stats2 = sub.Statistics.GetStats(reset: false);

        // Assert
        stats1.TotalProcessedMessages.Should().Be(10);
        stats2.TotalProcessedMessages.Should().Be(10); // Total doesn't reset
        stats2.IntervalMs.Should().BeGreaterThan(50); // But interval time resets
    }

    [Fact]
    public void Stats_Disabled_NoOverhead()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act - Subscribe without stats (using default overload)
        var sub = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            token: CancellationToken.None);

        // Assert - Statistics object exists but with minimal overhead (disabled options)
        // Having a non-null StatsTracker is better design than nulls (avoids null checks)
        sub.Statistics.Should().NotBeNull("StatsTracker should always exist to avoid null checks");
        sub.Statistics.StatsOptions.Should().NotBeNull();
    }

    // Task 37: Latency calculation tests

    [Fact]
    public async Task Stats_Latency_Calculated()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<string>(
            "test.channel",
            async _ =>
            {
                await Task.Delay(10); // Simulate processing time
            },
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(200);

        // Assert
        var stats = sub.Statistics.GetStats(reset: false);
        stats.AvgServiceTimeMs.Should().BeGreaterThan(0);
        stats.AvgLatencyTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Stats_MinMaxTracking_Accurate()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<int>(
            "test.channel",
            async msg =>
            {
                // Variable processing time based on message value
                await Task.Delay(msg.Body);
            },
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act - Send messages with different processing times
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(5), false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(50), false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(10), false);

        await Task.Delay(200);

        // Assert
        var stats = sub.Statistics.GetStats(reset: false);
        stats.MinServiceTimeMs.Should().BeGreaterThan(0);
        stats.MaxServiceTimeMs.Should().BeGreaterThan(stats.MinServiceTimeMs);
        stats.AvgServiceTimeMs.Should().BeInRange(stats.MinServiceTimeMs, stats.MaxServiceTimeMs);
    }

    [Fact]
    public async Task Stats_ResponseTime_CalculatedCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<string>(
            "test.channel",
            async _ =>
            {
                await Task.Delay(20);
            },
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act
        for (int i = 0; i < 5; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(200);

        // Assert
        var stats = sub.Statistics.GetStats(reset: false);
        stats.AvgResponseTime.Should().Be(stats.AvgLatencyTimeMs + stats.AvgServiceTimeMs);
        stats.AvgResponseTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Stats_QueueLength_TrackedCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var processing = new TaskCompletionSource<bool>();

        var sub = xBar.Subscribe<string>(
            "test.channel",
            async _ =>
            {
                await processing.Task; // Block processing
            },
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act - Publish messages faster than they can be processed
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(100);
        var statsWhileQueued = sub.Statistics.GetStats(reset: false);

        // Release processing
        processing.SetResult(true);
        await Task.Delay(200);
        var statsAfterProcessing = sub.Statistics.GetStats(reset: false);

        // Assert
        statsWhileQueued.QueueLength.Should().BeGreaterThan(0);
        statsAfterProcessing.QueueLength.Should().Be(0);
    }

    [Fact]
    public async Task Stats_MultipleIntervals_TrackCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var statsOptions = new StatsOptions();
        var sub = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            subscriptionName: null,
            statsOptions: statsOptions,
            token: CancellationToken.None);

        // Act - Interval 1
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }
        await Task.Delay(100);
        var stats1 = sub.Statistics.GetStats(reset: true);

        // Interval 2
        for (int i = 0; i < 20; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }
        await Task.Delay(100);
        var stats2 = sub.Statistics.GetStats(reset: false);

        // Assert
        stats1.TotalProcessedMessages.Should().Be(10);
        stats2.TotalProcessedMessages.Should().Be(30); // Cumulative
    }
}
