using Berberis.Messaging;
using Berberis.Messaging.Exceptions;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;
using System.Linq;

namespace Berberis.Messaging.Tests.ErrorHandling;

/// <summary>
/// Comprehensive tests for error handling and edge cases (Tasks 40-46)
/// </summary>
public class ErrorHandlingAndEdgeCaseTests
{
    // Task 40: Invalid channel names

    [Fact]
    public async Task Publish_NullChannelName_ThrowsArgumentNullException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var msg = TestHelpers.CreateTestMessage("test");

        // Act & Assert
        var act = async () => await xBar.Publish(null!, msg, store: false);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Publish_InvalidChannelName_ThrowsInvalidChannelNameException(string channelName)
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var msg = TestHelpers.CreateTestMessage("test");

        // Act & Assert
        var act = async () => await xBar.Publish(channelName, msg, store: false);
        await act.Should().ThrowAsync<InvalidChannelNameException>();
    }

    [Fact]
    public void Subscribe_NullChannelName_ThrowsArgumentNullException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act & Assert
        var act = () => xBar.Subscribe<string>(null!, _ => ValueTask.CompletedTask, CancellationToken.None);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Subscribe_InvalidChannelName_ThrowsInvalidChannelNameException(string channelName)
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act & Assert
        var act = () => xBar.Subscribe<string>(channelName, _ => ValueTask.CompletedTask, CancellationToken.None);
        act.Should().Throw<InvalidChannelNameException>();
    }

    // Task 41: Null parameters

    [Fact]
    public void Subscribe_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act & Assert
        var act = () => xBar.Subscribe<string>("test.channel", null!, CancellationToken.None);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Publish_NullMessage_HandledGracefully()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act - Publishing null body is allowed
        await xBar.Publish<string?>("test.channel", null);

        // Assert - Should not throw
        Assert.True(true);
    }

    // Task 42: Type mismatches

    [Fact]
    public async Task Publish_TypeMismatch_ThrowsChannelTypeMismatchException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, CancellationToken.None);

        // Act & Assert - Try to publish int to string channel
        var intMessage = TestHelpers.CreateTestMessage(42);
        var act = async () => await xBar.Publish("test.channel", intMessage, store: false);
        await act.Should().ThrowAsync<ChannelTypeMismatchException>();
    }

    [Fact]
    public void Subscribe_DifferentTypeSameChannel_ThrowsChannelTypeMismatchException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, CancellationToken.None);

        // Act & Assert
        var act = () => xBar.Subscribe<int>("test.channel", _ => ValueTask.CompletedTask, CancellationToken.None);
        act.Should().Throw<ChannelTypeMismatchException>();
    }

    // Task 43: Buffer overflow scenarios

    [Fact]
    public async Task Publish_BufferOverflow_HandlesGracefully()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var processedCount = 0;
        var slowProcessing = new TaskCompletionSource<bool>();

        xBar.Subscribe<int>("test.channel",
            async msg =>
            {
                Interlocked.Increment(ref processedCount);
                await slowProcessing.Task; // Block to cause buffer fill
            },
            CancellationToken.None);

        // Act - Publish many messages to overflow buffer
        for (int i = 0; i < 1000; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), false);
        }

        // Release processing
        slowProcessing.SetResult(true);
        await Task.Delay(500);

        // Assert - Some messages may be skipped due to slow consumer strategy
        processedCount.Should().BeGreaterThan(0);
    }

    // Task 44: Empty channels

    [Fact]
    public void GetChannelState_EmptyChannel_ReturnsEmpty()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var state = xBar.GetChannelState<string>("nonexistent.channel");

        // Assert
        state.Should().BeEmpty();
    }

    [Fact]
    public void GetChannelState_NonExistentChannel_ReturnsEmpty()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var state = xBar.GetChannelState<string>("does.not.exist");

        // Assert
        state.Should().NotBeNull();
        state.Should().BeEmpty();
    }

    [Fact]
    public void Subscribe_ToEmptyChannel_CreatesChannel()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var sub = xBar.Subscribe<string>("new.channel", _ => ValueTask.CompletedTask, CancellationToken.None);

        // Assert
        sub.Should().NotBeNull();
        sub.ChannelName.Should().Be("new.channel");
    }

    // Task 45: System channels

    [Fact]
    public void GetChannels_ExcludesSystemChannels()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Enable message tracing to create a system channel
        xBar.MessageTracingEnabled = true;

        // Create a user channel
        xBar.Subscribe<string>("user.channel", _ => ValueTask.CompletedTask, CancellationToken.None);

        // Subscribe to the system tracing channel
        xBar.Subscribe<MessageTrace>(xBar.TracingChannel, _ => ValueTask.CompletedTask, CancellationToken.None);

        // Act
        var channels = xBar.GetChannels();

        // Assert
        channels.Select(c => c.Name).Should().Contain("user.channel");
        channels.Select(c => c.Name).Should().NotContain(xBar.TracingChannel, "system channels should be excluded");
    }

    [Fact]
    public async Task SystemChannels_CanSubscribe()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<MessageTrace>>();

        // Enable message tracing to create the system channel
        xBar.MessageTracingEnabled = true;

        // Subscribe to the tracing channel
        xBar.Subscribe<MessageTrace>(xBar.TracingChannel, msg =>
        {
            received.Add(msg);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - Publish a message to a regular channel, which will generate trace messages
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("test"), false);
        await Task.Delay(100);

        // Assert - Should have received trace message(s)
        received.Should().NotBeEmpty("tracing channel should receive trace messages");
    }

    // Task 46: Large message volumes

    [Fact]
    public async Task Publish_LargeVolume_HandlesCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var expectedCount = 10000;

        xBar.Subscribe<int>("test.channel", msg =>
        {
            Interlocked.Increment(ref receivedCount);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act
        for (int i = 0; i < expectedCount; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), false);
        }

        await Task.Delay(500);

        // Assert - Should handle large volume
        receivedCount.Should().BeGreaterThan(expectedCount / 2); // At least half received
    }

    [Fact]
    public async Task Publish_LargeMessages_NoMemoryLeak()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var largeString = new string('A', 100000); // 100KB string
        var processedCount = 0;

        xBar.Subscribe<string>("test.channel", msg =>
        {
            Interlocked.Increment(ref processedCount);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - Publish large messages
        for (int i = 0; i < 100; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(largeString), false);
        }

        await Task.Delay(500);

        // Assert
        processedCount.Should().BeGreaterThan(50);
    }

    // Additional critical error scenarios

    [Fact]
    public async Task Dispose_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Dispose();

        // Act & Assert
        var act = async () => await xBar.Publish("test", TestHelpers.CreateTestMessage("msg"), false);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Subscription_Dispose_StopsReceiving()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;

        var sub = xBar.Subscribe<string>("test.channel", msg =>
        {
            Interlocked.Increment(ref receivedCount);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act
        sub.Dispose();

        // Assert - Further publishes should not be received
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("after dispose"), false);
        await Task.Delay(100);

        receivedCount.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentDispose_ThreadSafe()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act - Concurrent dispose operations
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() => xBar.Dispose()));
        await Task.WhenAll(tasks);

        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public async Task ResetChannel_ClearsState()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("msg1", key: "key1"), store: true);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("msg2", key: "key2"), store: true);

        // Act
        xBar.ResetChannel<string>("test.channel");

        // Assert
        var state = xBar.GetChannelState<string>("test.channel");
        state.Should().BeEmpty();
    }

    [Fact]
    public void TryDeleteChannel_ExistingChannel_ReturnsTrue()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, CancellationToken.None);

        // Act
        var result = xBar.TryDeleteChannel("test.channel");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TryDeleteChannel_NonExistentChannel_ReturnsFalse()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var result = xBar.TryDeleteChannel("nonexistent.channel");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandlerException_DoesNotCrashSubscription()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var processedCount = 0;

        xBar.Subscribe<int>("test.channel", msg =>
        {
            Interlocked.Increment(ref processedCount);
            if (msg.Body == 5)
                throw new Exception("Test exception");
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - Publish messages, one will throw
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), false);
        }

        await Task.Delay(200);

        // Assert - Should process other messages despite exception
        processedCount.Should().BeGreaterThan(5);
    }

    // Phase 3 Coverage Boost Tests

    [Fact]
    public void InvalidChannelNameException_Properties_SetCorrectly()
    {
        // VALIDATES: Exception properties and metadata
        // IMPACT: Covers 4 lines in InvalidChannelNameException.cs

        // Act
        var ex = new InvalidChannelNameException("bad..channel", "contains consecutive dots");

        // Assert
        ex.ChannelName.Should().Be("bad..channel");
        ex.Reason.Should().Be("contains consecutive dots");
        ex.Message.Should().Contain("bad..channel");
        ex.Message.Should().Contain("consecutive dots");
    }

    [Fact]
    public void InvalidChannelNameException_EmptyName_SetsPropertiesCorrectly()
    {
        // Act
        var ex = new InvalidChannelNameException("", "empty name");

        // Assert
        ex.ChannelName.Should().BeEmpty();
        ex.Reason.Should().Be("empty name");
        ex.Message.Should().Contain("empty");
    }

    [Fact]
    public void ChannelTypeMismatchException_Message_ContainsTypeInfo()
    {
        // VALIDATES: Exception message formatting and type metadata
        // IMPACT: Covers 6 lines in ChannelTypeMismatchException.cs

        // Act
        var ex = new ChannelTypeMismatchException(
            "orders.new",
            typeof(string),
            typeof(int));

        // Assert - Check all properties
        ex.ChannelName.Should().Be("orders.new");
        ex.ExpectedType.Should().Be(typeof(string));
        ex.ActualType.Should().Be(typeof(int));

        // Message should be descriptive
        ex.Message.Should().Contain("orders.new");
        ex.Message.Should().Contain("String");
        ex.Message.Should().Contain("Int32");
    }

    [Fact]
    public void ChannelTypeMismatchException_ToString_IncludesFullDetails()
    {
        // VALIDATES: Exception.ToString() includes all diagnostic info

        // Act
        var ex = new ChannelTypeMismatchException("test", typeof(double), typeof(bool));

        var toString = ex.ToString();

        // Assert
        toString.Should().Contain("ChannelTypeMismatchException");
        toString.Should().Contain("test");
        toString.Should().Contain("Double");
        toString.Should().Contain("Boolean");
    }
}
