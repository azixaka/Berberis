using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Berberis.Messaging;

namespace Berberis.Messaging.Tests;

public class SmokeTests
{
    [Fact]
    public void CrossBar_CanBeInstantiated()
    {
        // Arrange & Act
        var crossBar = new CrossBar(NullLoggerFactory.Instance);

        // Assert
        crossBar.Should().NotBeNull();
        crossBar.Should().BeAssignableTo<ICrossBar>();
    }

    [Fact]
    public void CrossBar_CanSubscribe()
    {
        // Arrange
        var crossBar = new CrossBar(NullLoggerFactory.Instance);

        // Act
        var subscription = crossBar.Subscribe<string>(
            "test.channel",
            msg => ValueTask.CompletedTask,
            default);

        // Assert
        subscription.Should().NotBeNull();
        subscription.ChannelName.Should().Be("test.channel");
    }

    [Fact]
    public async Task CrossBar_CanPublishAndReceive()
    {
        // Arrange
        var crossBar = new CrossBar(NullLoggerFactory.Instance);
        string? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        crossBar.Subscribe<string>(
            "test.channel",
            msg =>
            {
                receivedMessage = msg.Body;
                messageReceived.SetResult(true);
                return ValueTask.CompletedTask;
            },
            default);

        // Act
        await crossBar.Publish("test.channel", "Hello, World!");
        await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        receivedMessage.Should().Be("Hello, World!");
    }
}
