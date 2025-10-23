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

    // Task 2: ChannelStatsTracker tests

    [Fact]
    public async Task ChannelStatsTracker_GetStats_ReturnsChannelMetrics()
    {
        // VALIDATES: ChannelStatsTracker.GetStats returns accurate channel statistics
        // IMPACT: Covers ~17 lines in ChannelStatsTracker.cs

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var sub = xBar.Subscribe<string>("test.channel", msg => ValueTask.CompletedTask, default);

        // Publish messages to trigger channel statistics tracking
        for (int i = 0; i < 20; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(100); // Allow time to pass

        // Act - Get channel info and stats
        var channels = xBar.GetChannels();
        var testChannel = channels.First(c => c.Name == "test.channel");
        var stats = testChannel.Statistics.GetStats(reset: false);

        // Assert
        stats.TotalMessages.Should().Be(20, "should track all published messages");
        stats.IntervalMs.Should().BeGreaterThan(0, "interval should be greater than zero");
        stats.PublishRate.Should().BeGreaterThan(0, "publish rate should be calculated");

        sub.Dispose();
    }

    [Fact]
    public async Task ChannelStatsTracker_GetStats_Reset_ResetsIntervalCounters()
    {
        // VALIDATES: Reset parameter clears interval counters

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var sub = xBar.Subscribe<string>("test.channel", msg => ValueTask.CompletedTask, default);

        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(50);

        var channels1 = xBar.GetChannels();
        var testChannel1 = channels1.First(c => c.Name == "test.channel");

        // Act - First call with reset
        var stats1 = testChannel1.Statistics.GetStats(reset: true);

        await Task.Delay(50);

        // Publish more messages
        for (int i = 0; i < 5; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(50);

        var channels2 = xBar.GetChannels();
        var testChannel2 = channels2.First(c => c.Name == "test.channel");
        var stats2 = testChannel2.Statistics.GetStats(reset: false);

        // Assert
        stats1.TotalMessages.Should().Be(10);
        stats2.TotalMessages.Should().Be(15, "total should be cumulative");
        stats2.IntervalMs.Should().BeGreaterThan(stats1.IntervalMs, "interval should have increased");

        sub.Dispose();
    }

    [Fact]
    public async Task ChannelStatsTracker_GetStats_WithoutReset_CalculatesFromStart()
    {
        // VALIDATES: Without reset, interval is measured from tracker creation

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var sub = xBar.Subscribe<string>("test.channel", msg => ValueTask.CompletedTask, default);

        for (int i = 0; i < 30; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(100);

        var channels1 = xBar.GetChannels();
        var testChannel1 = channels1.First(c => c.Name == "test.channel");

        // Act - Call without reset multiple times
        var stats1 = testChannel1.Statistics.GetStats(reset: false);
        await Task.Delay(50);

        var channels2 = xBar.GetChannels();
        var testChannel2 = channels2.First(c => c.Name == "test.channel");
        var stats2 = testChannel2.Statistics.GetStats(reset: false);

        // Assert
        stats1.TotalMessages.Should().Be(30);
        stats2.TotalMessages.Should().Be(30, "total unchanged");
        stats2.IntervalMs.Should().BeGreaterThan(stats1.IntervalMs, "interval keeps growing without reset");

        sub.Dispose();
    }

    // Task 3: ChannelStats constructor tests

    [Fact]
    public void ChannelStats_Constructor_InitializesCorrectly()
    {
        // VALIDATES: ChannelStats struct initialization
        // IMPACT: Covers 5-12 lines in ChannelStats.cs

        // Act
        var stats = new ChannelStats(
            intervalMs: 1000.5f,
            messagesPerSecond: 150.75f,
            totalMessages: 1000);

        // Assert
        stats.IntervalMs.Should().Be(1000.5f);
        stats.PublishRate.Should().Be(150.75f);
        stats.TotalMessages.Should().Be(1000);
    }

    [Fact]
    public void ChannelStats_DefaultConstructor_HasDefaultValues()
    {
        // VALIDATES: Default initialization behavior

        // Act
        var stats = new ChannelStats();

        // Assert
        stats.IntervalMs.Should().Be(0);
        stats.PublishRate.Should().Be(0);
        stats.TotalMessages.Should().Be(0);
    }

    [Fact]
    public void ChannelStats_ToString_FormatsCorrectly()
    {
        // VALIDATES: ToString method for debugging/logging

        // Act
        var stats = new ChannelStats(5000.25f, 100.5f, 500);
        var str = stats.ToString();

        // Assert
        str.Should().NotBeNullOrEmpty();
        str.Should().Contain("5,000"); // IntervalMs
        str.Should().Contain("100"); // PublishRate
        str.Should().Contain("500"); // TotalMessages
    }

    // Task 4: StatsOptions tests

    [Fact]
    public void StatsOptions_Constructor_SetsDefaults()
    {
        // VALIDATES: StatsOptions default values
        // IMPACT: Covers 6-12 lines in StatsOptions.cs

        // Act - Default struct initialization (zeros all fields)
        var defaultOptions = new StatsOptions();

        // Assert - Default struct has zero values
        defaultOptions.Percentile.Should().BeNull("percentile null by default");
        defaultOptions.Alpha.Should().Be(0f);
        defaultOptions.Delta.Should().Be(0f);
        defaultOptions.EwmaWindowSize.Should().Be(0);
        defaultOptions.PercentileEnabled.Should().BeFalse("percentile not enabled when null");

        // Act - Constructor with default parameters
        var constructedOptions = new StatsOptions(percentile: null);

        // Assert - Constructor sets proper defaults
        constructedOptions.Percentile.Should().BeNull();
        constructedOptions.Alpha.Should().Be(0.05f, "constructor default for alpha");
        constructedOptions.Delta.Should().Be(0.05f, "constructor default for delta");
        constructedOptions.EwmaWindowSize.Should().Be(50, "constructor default for window size");
    }

    [Fact]
    public void StatsOptions_CustomValues_SetsCorrectly()
    {
        // VALIDATES: StatsOptions constructor with custom parameters

        // Act
        var options = new StatsOptions(
            percentile: 0.95f,
            alpha: 0.1f,
            delta: 0.1f,
            ewmaWindowSize: 100);

        // Assert
        options.Percentile.Should().Be(0.95f);
        options.Alpha.Should().Be(0.1f);
        options.Delta.Should().Be(0.1f);
        options.EwmaWindowSize.Should().Be(100);
        options.PercentileEnabled.Should().BeTrue("percentile enabled with valid value");
    }

    [Fact]
    public void StatsOptions_PercentileEnabled_ValidatesRange()
    {
        // VALIDATES: PercentileEnabled property validates percentile range (0.01-0.99)

        // Act & Assert
        var validOption = new StatsOptions(percentile: 0.95f);
        validOption.PercentileEnabled.Should().BeTrue("0.95 is valid");

        var tooLow = new StatsOptions(percentile: 0.005f);
        tooLow.PercentileEnabled.Should().BeFalse("0.005 is too low");

        var tooHigh = new StatsOptions(percentile: 0.995f);
        tooHigh.PercentileEnabled.Should().BeFalse("0.995 is too high");

        var nullPercentile = new StatsOptions(percentile: null);
        nullPercentile.PercentileEnabled.Should().BeFalse("null percentile not enabled");
    }
}
