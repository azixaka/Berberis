using Berberis.Messaging.Conflation;
using Berberis.Messaging.Statistics;
using FluentAssertions;
using System.Diagnostics;

namespace Berberis.Messaging.Tests.Conflation;

public class ConflationStrategyTests
{
    [Fact]
    public void FixedConflationStrategy_ReturnsFixedInterval()
    {
        // Arrange
        var interval = TimeSpan.FromMilliseconds(500);
        var strategy = new FixedConflationStrategy(interval);
        var statsTracker = new StatsTracker(new StatsOptions());
        strategy.Initialize(statsTracker);

        // Act
        var result1 = strategy.GetNextInterval(TimeSpan.FromMilliseconds(10));
        var result2 = strategy.GetNextInterval(TimeSpan.FromMilliseconds(100));
        var result3 = strategy.GetNextInterval(TimeSpan.Zero);

        // Assert
        result1.Should().Be(interval);
        result2.Should().Be(interval);
        result3.Should().Be(interval);
    }

    [Fact]
    public void AdaptiveConflationStrategy_WithQueueLengthSignal_AdaptBasedOnQueueLength()
    {
        // Arrange
        var strategy = new AdaptiveConflationStrategy
        {
            MinInterval = TimeSpan.FromMilliseconds(100),
            MaxInterval = TimeSpan.FromSeconds(5),
            Signal = AdaptiveSignal.QueueLength,
            QueueLengthThreshold = 1000,
            SmoothingFactor = 0.0 // No smoothing for predictable testing
        };

        var statsTracker = new StatsTracker(new StatsOptions());
        strategy.Initialize(statsTracker);

        // Simulate empty queue (no pressure)
        var interval1 = strategy.GetNextInterval(TimeSpan.Zero);

        // Simulate some messages enqueued
        for (int i = 0; i < 500; i++)
        {
            statsTracker.IncNumOfEnqueuedMessages();
        }

        var interval2 = strategy.GetNextInterval(TimeSpan.Zero);

        // Simulate high queue (max pressure)
        for (int i = 0; i < 1500; i++)
        {
            statsTracker.IncNumOfEnqueuedMessages();
        }

        var interval3 = strategy.GetNextInterval(TimeSpan.Zero);

        // Assert
        interval1.Should().Be(TimeSpan.FromMilliseconds(100)); // Min interval (no queue)
        interval2.Should().BeGreaterThan(interval1); // Increased interval (some queue)
        interval3.Should().BeCloseTo(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100)); // Max interval (high queue)
    }

    [Fact]
    public void AdaptiveConflationStrategy_WithFlushDurationSignal_AdaptBasedOnFlushDuration()
    {
        // Arrange
        var strategy = new AdaptiveConflationStrategy
        {
            MinInterval = TimeSpan.FromMilliseconds(100),
            MaxInterval = TimeSpan.FromSeconds(2),
            Signal = AdaptiveSignal.FlushDuration,
            FlushDurationThresholdMs = 100.0,
            SmoothingFactor = 0.0
        };

        var statsTracker = new StatsTracker(new StatsOptions());
        strategy.Initialize(statsTracker);

        // Record multiple samples of low flush duration to establish baseline
        var ticks5ms = (long)(5 * (Stopwatch.Frequency / 1000.0));
        for (int i = 0; i < 10; i++)
        {
            statsTracker.RecordFlushDuration(ticks5ms);
        }
        var interval1 = strategy.GetNextInterval(TimeSpan.FromMilliseconds(5));

        // Record multiple samples of high flush duration
        var ticks200ms = (long)(200 * (Stopwatch.Frequency / 1000.0));
        for (int i = 0; i < 20; i++)
        {
            statsTracker.RecordFlushDuration(ticks200ms);
        }
        var interval2 = strategy.GetNextInterval(TimeSpan.FromMilliseconds(200));

        // Assert - intervals should increase with flush duration
        interval1.Should().BeLessThan(TimeSpan.FromMilliseconds(400)); // Low pressure
        interval2.Should().BeGreaterThan(TimeSpan.FromSeconds(1)); // High pressure (200ms > 100ms threshold)
        interval2.Should().BeGreaterThan(interval1); // Definitely increased
    }

    [Fact]
    public void AdaptiveConflationStrategy_WithSmoothing_ChangesGradually()
    {
        // Arrange
        var strategy = new AdaptiveConflationStrategy
        {
            MinInterval = TimeSpan.FromMilliseconds(100),
            MaxInterval = TimeSpan.FromSeconds(5),
            Signal = AdaptiveSignal.QueueLength,
            QueueLengthThreshold = 1000,
            SmoothingFactor = 0.7 // High smoothing
        };

        var statsTracker = new StatsTracker(new StatsOptions());
        strategy.Initialize(statsTracker);

        // Start with empty queue
        var interval1 = strategy.GetNextInterval(TimeSpan.Zero);

        // Suddenly add high queue
        for (int i = 0; i < 2000; i++)
        {
            statsTracker.IncNumOfEnqueuedMessages();
        }

        var interval2 = strategy.GetNextInterval(TimeSpan.Zero);
        var interval3 = strategy.GetNextInterval(TimeSpan.Zero);
        var interval4 = strategy.GetNextInterval(TimeSpan.Zero);

        // Assert - should gradually increase due to smoothing
        interval2.Should().BeGreaterThan(interval1);
        interval3.Should().BeGreaterThan(interval2);
        interval4.Should().BeGreaterThan(interval3);

        // But shouldn't jump immediately to max
        interval2.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AdaptiveConflationStrategy_CombinedSignal_UsesWeightedAverage()
    {
        // Arrange
        var strategy = new AdaptiveConflationStrategy
        {
            MinInterval = TimeSpan.FromMilliseconds(100),
            MaxInterval = TimeSpan.FromSeconds(5),
            Signal = AdaptiveSignal.Combined,
            SmoothingFactor = 0.0
        };

        var statsTracker = new StatsTracker(new StatsOptions());
        strategy.Initialize(statsTracker);

        // Simulate some load
        for (int i = 0; i < 500; i++)
        {
            statsTracker.IncNumOfEnqueuedMessages();
        }

        var ticks50ms = (long)(50 * (Stopwatch.Frequency / 1000.0));
        statsTracker.RecordFlushDuration(ticks50ms);

        var interval = strategy.GetNextInterval(TimeSpan.FromMilliseconds(50));

        // Assert - should be somewhere in the middle based on combined signals
        interval.Should().BeGreaterThan(TimeSpan.FromMilliseconds(100));
        interval.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}
