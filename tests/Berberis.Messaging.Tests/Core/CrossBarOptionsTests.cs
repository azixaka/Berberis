using FluentAssertions;
using Berberis.Messaging.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Berberis.Messaging.Tests.Core;

public class CrossBarOptionsTests
{
    [Fact]
    public void CrossBarOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new CrossBarOptions();

        // Assert
        options.DefaultBufferCapacity.Should().BeNull();  // Unbounded by default
        options.DefaultSlowConsumerStrategy.Should().Be(SlowConsumerStrategy.SkipUpdates);
        options.DefaultConflationInterval.Should().Be(TimeSpan.Zero);
        options.MaxChannels.Should().BeNull();
        options.MaxChannelNameLength.Should().Be(256);
        options.EnableMessageTracing.Should().BeFalse();
        options.EnablePublishLogging.Should().BeFalse();
        options.SystemChannelPrefix.Should().Be("$");
        options.SystemChannelBufferCapacity.Should().Be(1000);  // Original hardcoded value
    }

    [Fact]
    public void Validate_WithDefaultOptions_Succeeds()
    {
        // Arrange
        var options = new CrossBarOptions();

        // Act & Assert
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNegativeBufferCapacity_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultBufferCapacity = -1
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*DefaultBufferCapacity*greater than 0*");
    }

    [Fact]
    public void Validate_WithZeroBufferCapacity_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultBufferCapacity = 0
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*DefaultBufferCapacity*greater than 0*");
    }

    [Fact]
    public void Validate_WithNegativeMaxChannels_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            MaxChannels = -1
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxChannels*greater than 0*");
    }

    [Fact]
    public void Validate_WithZeroMaxChannels_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            MaxChannels = 0
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxChannels*greater than 0*");
    }

    [Fact]
    public void Validate_WithNegativeMaxChannelNameLength_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            MaxChannelNameLength = -1
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxChannelNameLength*greater than 0*");
    }

    [Fact]
    public void Validate_WithNegativeConflationInterval_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultConflationInterval = TimeSpan.FromSeconds(-1)
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*DefaultConflationInterval*cannot be negative*");
    }

    [Fact]
    public void Validate_WithNullSystemChannelPrefix_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            SystemChannelPrefix = null!
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SystemChannelPrefix*");
    }

    [Fact]
    public void Validate_WithEmptySystemChannelPrefix_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            SystemChannelPrefix = ""
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SystemChannelPrefix*");
    }

    [Fact]
    public void Validate_WithWhitespaceSystemChannelPrefix_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            SystemChannelPrefix = "   "
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SystemChannelPrefix*");
    }

    [Fact]
    public void Validate_WithNegativeSystemChannelBufferCapacity_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            SystemChannelBufferCapacity = -1
        };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SystemChannelBufferCapacity*greater than 0*");
    }

    [Fact]
    public void CrossBar_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options: null);

        // Assert
        crossBar.Should().NotBeNull();
        crossBar.MessageTracingEnabled.Should().BeFalse();
        crossBar.PublishLoggingEnabled.Should().BeFalse();
    }

    [Fact]
    public void CrossBar_WithCustomOptions_AppliesOptions()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            EnableMessageTracing = true,
            EnablePublishLogging = true,
            SystemChannelPrefix = "#"
        };

        // Act
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options);

        // Assert
        crossBar.MessageTracingEnabled.Should().BeTrue();
        crossBar.PublishLoggingEnabled.Should().BeTrue();
        crossBar.TracingChannel.Should().Be("#message.traces");
    }

    [Fact]
    public void CrossBar_WithInvalidOptions_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultBufferCapacity = -1
        };

        // Act & Assert
        var act = () => new CrossBar(NullLoggerFactory.Instance, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Subscribe_WithNullDefaultBufferCapacity_CreatesUnboundedChannel()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultBufferCapacity = null  // Unbounded (default)
        };
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options);
        var received = new List<Message<string>>();
        var mre = new ManualResetEventSlim();

        // Act
        var subscription = crossBar.Subscribe<string>("test.channel", msg =>
        {
            received.Add(msg);
            mre.Set();
            return ValueTask.CompletedTask;
        }, default);

        await crossBar.Publish("test.channel", "test");

        // Assert
        mre.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        received.Should().HaveCount(1);
    }

    [Fact]
    public async Task Subscribe_WithExplicitDefaultBufferCapacity_CreatesBoundedChannel()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultBufferCapacity = 5000  // Bounded
        };
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options);
        var received = new List<Message<string>>();
        var mre = new ManualResetEventSlim();

        // Act
        var subscription = crossBar.Subscribe<string>("test.channel", msg =>
        {
            received.Add(msg);
            mre.Set();
            return ValueTask.CompletedTask;
        }, default);

        await crossBar.Publish("test.channel", "test");

        // Assert
        mre.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        received.Should().HaveCount(1);
    }

    [Fact]
    public async Task Subscribe_WithDefaultConflationInterval_SubscriptionSucceeds()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            DefaultConflationInterval = TimeSpan.FromMilliseconds(100)
        };
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options);
        var received = new List<Message<string>>();
        var mre = new ManualResetEventSlim();

        // Act - Subscribe without explicitly setting conflation interval
        var subscription = crossBar.Subscribe<string>("test.channel", msg =>
        {
            received.Add(msg);
            mre.Set();
            return ValueTask.CompletedTask;
        }, default);

        await crossBar.Publish("test.channel", "test");

        // Assert - Verify subscription works and message is received
        mre.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        received.Should().HaveCount(1);
        subscription.ConflationInterval.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Subscribe_ExceedsMaxChannels_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            MaxChannels = 2
        };
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options);

        // Act - Create 2 channels (should succeed)
        crossBar.Subscribe<string>("channel1", _ => ValueTask.CompletedTask, default);
        crossBar.Subscribe<string>("channel2", _ => ValueTask.CompletedTask, default);

        // Act & Assert - Third channel should fail
        var act = () => crossBar.Subscribe<string>("channel3", _ => ValueTask.CompletedTask, default);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Maximum number of channels*");
    }

    [Fact]
    public void Publish_ExceedsMaxChannelNameLength_Throws()
    {
        // Arrange
        var options = new CrossBarOptions
        {
            MaxChannelNameLength = 10
        };
        var crossBar = new CrossBar(NullLoggerFactory.Instance, options);
        var longChannelName = new string('a', 11);

        // Act & Assert
        var act = async () => await crossBar.Publish(longChannelName, "test");
        act.Should().ThrowAsync<InvalidChannelNameException>()
            .WithMessage("*too long*10*");
    }

}
