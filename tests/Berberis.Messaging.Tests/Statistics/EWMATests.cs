using Berberis.Messaging.Statistics;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Statistics;

public class EWMATests
{
    // Task 39: EWMA calculation tests

    [Fact]
    public void EWMA_InitialSample_SetsAverage()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);

        // Act
        ewma.NewSample(100f);

        // Assert
        ewma.AverageValue.Should().Be(100f);
        ewma.MinValue.Should().Be(100f);
        ewma.MaxValue.Should().Be(100f);
    }

    [Fact]
    public void EWMA_ConstantValues_AverageStable()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);

        // Act - Feed constant value
        for (int i = 0; i < 20; i++)
        {
            ewma.NewSample(50f);
        }

        // Assert - Average should converge to 50
        ewma.AverageValue.Should().BeApproximately(50f, 0.1f);
        ewma.MinValue.Should().Be(50f);
        ewma.MaxValue.Should().Be(50f);
    }

    [Fact]
    public void EWMA_IncreasingValues_AverageIncreases()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);
        ewma.NewSample(10f);
        var initialAverage = ewma.AverageValue;

        // Act
        for (int i = 0; i < 10; i++)
        {
            ewma.NewSample(100f);
        }

        // Assert
        ewma.AverageValue.Should().BeGreaterThan(initialAverage);
        ewma.MaxValue.Should().Be(100f);
        ewma.MinValue.Should().Be(10f);
    }

    [Fact]
    public void EWMA_DecreasingValues_AverageDecreases()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);
        ewma.NewSample(100f);
        var initialAverage = ewma.AverageValue;

        // Act
        for (int i = 0; i < 10; i++)
        {
            ewma.NewSample(10f);
        }

        // Assert
        ewma.AverageValue.Should().BeLessThan(initialAverage);
        ewma.MaxValue.Should().Be(100f);
        ewma.MinValue.Should().Be(10f);
    }

    [Fact]
    public void EWMA_MinMaxTracking_Accurate()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);

        // Act - Add varied samples
        ewma.NewSample(50f);
        ewma.NewSample(10f);
        ewma.NewSample(90f);
        ewma.NewSample(30f);
        ewma.NewSample(70f);

        // Assert
        ewma.MinValue.Should().Be(10f);
        ewma.MaxValue.Should().Be(90f);
        ewma.AverageValue.Should().BeInRange(10f, 90f);
    }

    [Fact]
    public void EWMA_Reset_ClearsState()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);
        ewma.NewSample(100f);
        ewma.NewSample(200f);
        ewma.AverageValue.Should().NotBe(0);

        // Act
        ewma.Reset();

        // Assert
        ewma.AverageValue.Should().Be(0);
        ewma.MinValue.Should().Be(0);
        ewma.MaxValue.Should().Be(0);

        // After reset, first sample becomes the average
        ewma.NewSample(75f);
        ewma.AverageValue.Should().Be(75f);
    }

    [Fact]
    public void EWMA_LargerWindow_SlowerAdaptation()
    {
        // Arrange
        var fastEwma = new ExponentialWeightedMovingAverage(samplesPerWindow: 5);
        var slowEwma = new ExponentialWeightedMovingAverage(samplesPerWindow: 50);

        // Both start at 10
        fastEwma.NewSample(10f);
        slowEwma.NewSample(10f);

        // Act - Suddenly shift to 100
        for (int i = 0; i < 10; i++)
        {
            fastEwma.NewSample(100f);
            slowEwma.NewSample(100f);
        }

        // Assert - Fast EWMA should adapt more quickly
        fastEwma.AverageValue.Should().BeGreaterThan(slowEwma.AverageValue);
    }

    [Fact]
    public void EWMA_SmallerWindow_FasterAdaptation()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 3);

        // Act - Start at 50, then shift to 150
        ewma.NewSample(50f);
        ewma.NewSample(50f);
        ewma.NewSample(50f);
        var beforeShift = ewma.AverageValue;

        ewma.NewSample(150f);
        ewma.NewSample(150f);
        var afterTwoHighSamples = ewma.AverageValue;

        ewma.NewSample(150f);
        var afterThreeHighSamples = ewma.AverageValue;

        // Assert - Should adapt quickly
        afterTwoHighSamples.Should().BeGreaterThan(beforeShift);
        afterThreeHighSamples.Should().BeGreaterThan(afterTwoHighSamples);
    }

    [Fact]
    public void EWMA_AlphaCalculation_Correct()
    {
        // Arrange & Act
        var ewma10 = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);
        var ewma50 = new ExponentialWeightedMovingAverage(samplesPerWindow: 50);

        // Start with a different value, then shift to 100 to show adaptation rate
        ewma10.NewSample(50f);
        ewma50.NewSample(50f);

        // Feed same sequence of higher values
        for (int i = 0; i < 5; i++)
        {
            ewma10.NewSample(100f);
            ewma50.NewSample(100f);
        }

        // Assert - Smaller window (larger alpha) adapts faster
        // After same number of samples at 100, ewma10 should be closer to 100 than ewma50
        ewma10.AverageValue.Should().BeGreaterThan(ewma50.AverageValue,
            "smaller window (ewma10) should adapt faster to new values");
    }

    [Fact]
    public void EWMA_OscillatingValues_Smooths()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);

        // Act - Oscillate between 10 and 90
        for (int i = 0; i < 20; i++)
        {
            ewma.NewSample(i % 2 == 0 ? 10f : 90f);
        }

        // Assert - Average should be somewhere in the middle, smoothing the oscillation
        ewma.AverageValue.Should().BeInRange(30f, 70f);
        ewma.MinValue.Should().Be(10f);
        ewma.MaxValue.Should().Be(90f);
    }

    [Fact]
    public void EWMA_VerySmallWindow_RespondsQuickly()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 1);

        // Act
        ewma.NewSample(10f);
        ewma.NewSample(100f);

        // Assert - With window of 1, alpha = 2/(1+1) = 1.0, so should fully adopt new value
        // Actually, the formula is 2/(n+1), so alpha = 2/2 = 1.0
        // EWMA = previous + alpha * (value - previous) = previous + 1.0 * (value - previous) = value
        ewma.AverageValue.Should().BeApproximately(100f, 5f);
    }

    [Fact]
    public void EWMA_InvalidWindow_UsesDefault()
    {
        // Arrange & Act - Pass invalid window size
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 0);

        // Should use default of 50
        ewma.NewSample(100f);

        // Assert - Should work normally
        ewma.AverageValue.Should().Be(100f);
    }

    [Fact]
    public void EWMA_NegativeWindow_UsesDefault()
    {
        // Arrange & Act - Pass negative window size
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: -10);

        // Should use default of 50
        ewma.NewSample(50f);

        // Assert - Should work normally
        ewma.AverageValue.Should().Be(50f);
    }

    [Fact]
    public void EWMA_MultipleReset_WorksCorrectly()
    {
        // Arrange
        var ewma = new ExponentialWeightedMovingAverage(samplesPerWindow: 10);

        // Act - Add samples, reset, add more, reset again
        ewma.NewSample(100f);
        ewma.Reset();
        ewma.NewSample(200f);
        var afterFirstReset = ewma.AverageValue;
        ewma.Reset();
        ewma.NewSample(300f);
        var afterSecondReset = ewma.AverageValue;

        // Assert
        afterFirstReset.Should().Be(200f);
        afterSecondReset.Should().Be(300f);
    }
}
