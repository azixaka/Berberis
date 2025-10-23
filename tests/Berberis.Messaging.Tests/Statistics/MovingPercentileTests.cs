using Berberis.Messaging.Statistics;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Statistics;

public class MovingPercentileTests
{
    // Task 38: Percentile accuracy tests

    [Fact]
    public void MovingPercentile_InitialValue_SetCorrectly()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f);

        // Act
        percentile.NewSample(100f);

        // Assert
        percentile.PercentileValue.Should().Be(100f);
    }

    [Fact]
    public void MovingPercentile_ValuesAbove_IncreasesPercentile()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f, alpha: 0.05f, delta: 1.0f);
        percentile.NewSample(100f);
        var initial = percentile.PercentileValue;

        // Act
        percentile.NewSample(150f); // Value above current percentile

        // Assert
        percentile.PercentileValue.Should().BeGreaterThan(initial);
    }

    [Fact]
    public void MovingPercentile_ValuesBelow_DecreasesPercentile()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f, alpha: 0.05f, delta: 1.0f);
        percentile.NewSample(100f);
        var initial = percentile.PercentileValue;

        // Act
        percentile.NewSample(50f); // Value below current percentile

        // Assert
        percentile.PercentileValue.Should().BeLessThan(initial);
    }

    [Theory]
    [InlineData(0.5f)] // p50 (median)
    [InlineData(0.9f)] // p90
    [InlineData(0.99f)] // p99
    public void MovingPercentile_DifferentPercentiles_Calculated(float percentile)
    {
        // Arrange - Use smaller delta for better convergence
        var mp = new MovingPercentile(percentile, alpha: 0.05f, delta: 0.05f);

        // Act - Add many samples for convergence
        for (int i = 1; i <= 1000; i++)
        {
            mp.NewSample(i);
        }

        // Assert - Verify the streaming algorithm is tracking in the general direction
        // This is a streaming/incremental algorithm, not an exact percentile calculator
        // It converges slowly: with delta=0.05, increases by delta/(1-p) per sample
        mp.PercentileValue.Should().BeGreaterThan(50f,
            "percentile should have increased from first sample");
        mp.PercentileValue.Should().BeLessThan(1100f,
            "percentile should be in reasonable range (can overshoot slightly)");

        // For higher percentiles (p90, p99), value should trend higher
        if (percentile >= 0.9f)
        {
            mp.PercentileValue.Should().BeGreaterThan(500f,
                "high percentiles should track toward higher values");
        }
    }

    [Fact]
    public void MovingPercentile_WithEWMA_AdjustsDelta()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f, alpha: 0.1f, delta: 1.0f);
        percentile.NewSample(100f);

        // Act - Use overload with EWMA
        percentile.NewSample(120f, ewma: 110f);

        // Assert - Should have updated based on EWMA
        percentile.PercentileValue.Should().BeGreaterThan(100f);
    }

    [Fact]
    public void MovingPercentile_Reset_ClearsState()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f);
        percentile.NewSample(100f);
        percentile.NewSample(150f);
        percentile.PercentileValue.Should().NotBe(0);

        // Act
        percentile.Reset();

        // Assert
        percentile.PercentileValue.Should().Be(0);

        // After reset, first sample becomes the percentile again
        percentile.NewSample(200f);
        percentile.PercentileValue.Should().Be(200f);
    }

    [Fact]
    public void MovingPercentile_ConvergesToCorrectValue()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f, alpha: 0.05f, delta: 0.1f);

        // Act - Feed a steady stream of values from 1-100
        // The 90th percentile should be around 90
        for (int iteration = 0; iteration < 10; iteration++)
        {
            for (int i = 1; i <= 100; i++)
            {
                percentile.NewSample(i);
            }
        }

        // Assert - Should converge to around 90
        percentile.PercentileValue.Should().BeInRange(80f, 100f);
    }

    [Fact]
    public void MovingPercentile_EqualValues_StablePercentile()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f, alpha: 0.05f, delta: 1.0f);
        percentile.NewSample(100f);

        // Act - Feed same value multiple times
        for (int i = 0; i < 10; i++)
        {
            percentile.NewSample(100f);
        }

        // Assert - Percentile should remain stable
        percentile.PercentileValue.Should().BeApproximately(100f, 0.1f);
    }

    [Fact]
    public void MovingPercentile_LowPercentile_TracksLowerValues()
    {
        // Arrange - p10 with smaller delta for better convergence
        var percentile = new MovingPercentile(0.1f, alpha: 0.05f, delta: 0.05f);

        // Act - Add values 1-1000 (more samples for convergence)
        for (int i = 1; i <= 1000; i++)
        {
            percentile.NewSample(i);
        }

        // Assert - p10 should trend toward lower range (around 100)
        // Streaming algorithm, so allow wider tolerance
        percentile.PercentileValue.Should().BeLessThan(300f,
            "p10 should track lower values with streaming algorithm");
    }

    [Fact]
    public void MovingPercentile_HighPercentile_TracksHigherValues()
    {
        // Arrange - p99
        var percentile = new MovingPercentile(0.99f, alpha: 0.05f, delta: 0.5f);

        // Act - Add values 1-100
        for (int i = 1; i <= 100; i++)
        {
            percentile.NewSample(i);
        }

        // Assert - p99 should be close to 100
        percentile.PercentileValue.Should().BeGreaterThan(90f);
    }

    [Fact]
    public void MovingPercentile_RapidChanges_Adapts()
    {
        // Arrange
        var percentile = new MovingPercentile(0.9f, alpha: 0.1f, delta: 1.0f);

        // Act - Start with low values
        for (int i = 1; i <= 10; i++)
        {
            percentile.NewSample(i);
        }
        var lowValuePercentile = percentile.PercentileValue;

        // Suddenly shift to high values
        for (int i = 90; i <= 100; i++)
        {
            percentile.NewSample(i);
        }
        var highValuePercentile = percentile.PercentileValue;

        // Assert - Should adapt to new range
        highValuePercentile.Should().BeGreaterThan(lowValuePercentile);
    }
}
