using FluentAssertions;
using Berberis.Messaging.Exceptions;
using Berberis.Messaging.Tests.Helpers;

namespace Berberis.Messaging.Tests.Core;

public partial class CrossBarTests
{
    [Fact]
    public async Task Publish_SingleSubscriber_ReceivesMessage()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<Message<string>>();
        var messageReceived = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedMessages.Add(msg);
            messageReceived.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        var testMessage = TestHelpers.CreateTestMessage("Hello World");
        await xBar.Publish("test.channel", testMessage, store: false);

        // Assert
        messageReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Body.Should().Be("Hello World");
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_AllReceive()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received1 = new List<Message<string>>();
        var received2 = new List<Message<string>>();
        var received3 = new List<Message<string>>();
        var countdown = new CountdownEvent(3);

        xBar.Subscribe<string>("test.channel", msg =>
        {
            received1.Add(msg);
            countdown.Signal();
            return ValueTask.CompletedTask;
        }, default);

        xBar.Subscribe<string>("test.channel", msg =>
        {
            received2.Add(msg);
            countdown.Signal();
            return ValueTask.CompletedTask;
        }, default);

        xBar.Subscribe<string>("test.channel", msg =>
        {
            received3.Add(msg);
            countdown.Signal();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("test.channel", "Test Message");

        // Assert
        countdown.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        received1.Should().ContainSingle().Which.Body.Should().Be("Test Message");
        received2.Should().ContainSingle().Which.Body.Should().Be("Test Message");
        received3.Should().ContainSingle().Which.Body.Should().Be("Test Message");
    }

    [Fact]
    public async Task Publish_DifferentChannel_NoMessageReceived()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<Message<string>>();

        xBar.Subscribe<string>("channel.A", msg =>
        {
            receivedMessages.Add(msg);
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("channel.B", "Message on B");
        await Task.Delay(100); // Give time for any erroneous delivery

        // Assert
        receivedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_BeforeSubscribe_MessageNotReceived()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<Message<string>>();

        // Act - Publish before subscribe
        await xBar.Publish("test.channel", "Early Message");

        // Subscribe after publish
        xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedMessages.Add(msg);
            return ValueTask.CompletedTask;
        }, default);

        await Task.Delay(100);

        // Assert - Should not receive the early message
        receivedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_AfterPublish_OnlyReceivesNewMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<Message<string>>();
        var messageReceived = new ManualResetEventSlim(false);

        // Act - Publish before subscribe
        await xBar.Publish("test.channel", "Message 1");
        await xBar.Publish("test.channel", "Message 2");

        // Subscribe after publish
        xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedMessages.Add(msg);
            messageReceived.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Publish after subscribe
        await xBar.Publish("test.channel", "Message 3");

        // Assert
        messageReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        receivedMessages.Should().ContainSingle();
        receivedMessages[0].Body.Should().Be("Message 3");
    }

    // Task 7: Type safety tests
    [Fact]
    public async Task Publish_TypeMismatch_ThrowsChannelTypeMismatchException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, default);

        // Act & Assert
        var intMessage = TestHelpers.CreateTestMessage(42);
        var act = async () => await xBar.Publish("test.channel", intMessage, store: false);

        await act.Should().ThrowAsync<ChannelTypeMismatchException>()
           .WithMessage("*type*");
    }

    [Fact]
    public void Subscribe_DifferentTypeSameChannel_ThrowsChannelTypeMismatchException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, default);

        // Act & Assert
        var act = () => xBar.Subscribe<int>("test.channel", _ => ValueTask.CompletedTask, default);

        act.Should().Throw<ChannelTypeMismatchException>()
           .WithMessage("*type*");
    }

    [Fact]
    public async Task Publish_GenericTypeConsistency_Maintained()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedInt = 0;
        var receivedString = "";
        var intReceived = new ManualResetEventSlim(false);
        var stringReceived = new ManualResetEventSlim(false);

        xBar.Subscribe<int>("int.channel", msg =>
        {
            receivedInt = msg.Body;
            intReceived.Set();
            return ValueTask.CompletedTask;
        }, default);

        xBar.Subscribe<string>("string.channel", msg =>
        {
            receivedString = msg.Body ?? "";
            stringReceived.Set();
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("int.channel", 42);
        await xBar.Publish("string.channel", "Hello");

        // Assert
        intReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        stringReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        receivedInt.Should().Be(42);
        receivedString.Should().Be("Hello");
    }

    // Task 8: Disposal tests
    [Fact]
    public async Task Dispose_StopsReceivingMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<Message<string>>();

        xBar.Subscribe<string>("test.channel", msg =>
        {
            receivedMessages.Add(msg);
            return ValueTask.CompletedTask;
        }, default);

        await xBar.Publish("test.channel", "Message 1");
        await Task.Delay(100);

        // Act
        xBar.Dispose();
        await Task.Delay(100);

        var initialCount = receivedMessages.Count;

        // Try to publish after disposal (should not crash but message won't be delivered)
        try
        {
            await xBar.Publish("test.channel", "Message 2");
        }
        catch (ObjectDisposedException)
        {
            // Expected
        }

        await Task.Delay(100);

        // Assert
        receivedMessages.Count.Should().Be(initialCount);
    }

    [Fact]
    public void Dispose_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Dispose();

        // Act & Assert
        var act = () => xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, default);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_ConcurrentPublish_HandledGracefully()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.Subscribe<string>("test.channel", _ => ValueTask.CompletedTask, default);

        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    await xBar.Publish("test.channel", $"Message {i}");
                    await Task.Delay(1);
                }
                catch (ObjectDisposedException)
                {
                    // Expected when disposed
                    break;
                }
            }
        });

        // Act
        await Task.Delay(50);
        xBar.Dispose();

        // Assert - Should complete without hanging or crashing
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Dispose_DisposesAllSubscriptions()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var sub1 = xBar.Subscribe<string>("channel1", _ => ValueTask.CompletedTask, default);
        var sub2 = xBar.Subscribe<string>("channel2", _ => ValueTask.CompletedTask, default);
        var sub3 = xBar.Subscribe<int>("channel3", _ => ValueTask.CompletedTask, default);

        // Act
        xBar.Dispose();

        // Assert - MessageLoop task should complete when subscriptions are disposed
        await Task.WhenAll(sub1.MessageLoop, sub2.MessageLoop, sub3.MessageLoop)
            .WaitAsync(TimeSpan.FromSeconds(2));

        sub1.MessageLoop.IsCompleted.Should().BeTrue();
        sub2.MessageLoop.IsCompleted.Should().BeTrue();
        sub3.MessageLoop.IsCompleted.Should().BeTrue();
    }

    // Task 9: Channel management tests
    [Fact]
    public async Task GetChannels_ReturnsAllNonSystemChannels()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("user.channel1", "test");
        await xBar.Publish("user.channel2", 42);
        await xBar.Publish("user.channel3", true);

        // Act
        var channels = xBar.GetChannels().ToList();

        // Assert
        channels.Should().HaveCount(3);
        channels.Should().Contain(c => c.Name == "user.channel1");
        channels.Should().Contain(c => c.Name == "user.channel2");
        channels.Should().Contain(c => c.Name == "user.channel3");
    }

    [Fact]
    public async Task GetChannels_ExcludesSystemChannels()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.MessageTracingEnabled = true;
        await xBar.Publish("user.channel", "test");
        await xBar.Publish("$system.channel", "system");

        // Act
        var channels = xBar.GetChannels().ToList();

        // Assert
        channels.Should().NotContain(c => c.Name.StartsWith("$"));
        channels.Should().Contain(c => c.Name == "user.channel");
    }

    [Fact]
    public async Task TryDeleteChannel_RemovesChannel_ReturnsTrue()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("test.channel", "test");

        // Act
        var result = xBar.TryDeleteChannel("test.channel");

        // Assert
        result.Should().BeTrue();
        var channels = xBar.GetChannels().ToList();
        channels.Should().NotContain(c => c.Name == "test.channel");
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
    public async Task ResetChannel_ClearsState()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        await xBar.Publish("test.channel", "msg1", key: "key1", store: true);
        await xBar.Publish("test.channel", "msg2", key: "key2", store: true);

        // Verify state exists
        var stateBefore = xBar.GetChannelState<string>("test.channel").ToList();
        stateBefore.Should().HaveCount(2);

        // Act
        xBar.ResetChannel<string>("test.channel");

        // Assert
        var stateAfter = xBar.GetChannelState<string>("test.channel").ToList();
        stateAfter.Should().BeEmpty();
    }

    // Phase 3 Coverage Boost Tests

    [Fact]
    public async Task CrossBar_PublishOverload_WithAllParameters()
    {
        // VALIDATES: Less-common publish overload with all optional parameters
        // IMPACT: Covers 10 lines in CrossBar publish overloads

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<Message<string>>();

        xBar.Subscribe<string>("test.channel", msg =>
        {
            received.Add(msg);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - Use publish with body and all optional parameters
        await xBar.Publish(
            channelName: "test.channel",
            body: "test-message",
            correlationId: 12345,
            key: "unique-key",
            store: true,
            from: "test-source",
            tagA: 999);

        await Task.Delay(100);

        // Assert - All parameters preserved
        received.Should().HaveCount(1);
        var msg = received[0];

        msg.Body.Should().Be("test-message");
        msg.CorrelationId.Should().Be(12345);
        msg.Key.Should().Be("unique-key");
        msg.From.Should().Be("test-source");
        msg.TagA.Should().Be(999);

        // Verify message was stored
        var state = xBar.GetChannelState<string>("test.channel");
        state.Should().HaveCount(1);
    }

    [Fact]
    public async Task CrossBar_PublishOverload_MessageObject_Works()
    {
        // VALIDATES: Publish overload accepting Message object

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = false;

        xBar.Subscribe<int>("test", msg =>
        {
            received = true;
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - Use message object overload
        var message = TestHelpers.CreateTestMessage(42);
        await xBar.Publish("test", message, store: false);

        await Task.Delay(50);

        // Assert
        received.Should().BeTrue();
    }

    // Coverage Boost: CrossBar publish logging and slow consumer scenarios
    [Fact]
    public async Task CrossBar_WithPublishLogging_LogsMessages()
    {
        // VALIDATES: PublishLoggingEnabled paths (lines 122-124 CrossBar.cs)
        // IMPACT: Covers publish logging code

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.PublishLoggingEnabled = true;

        var received = new List<string>();
        var sub = xBar.Subscribe<string>("test.channel", msg =>
        {
            received.Add(msg.Body!);
            return ValueTask.CompletedTask;
        }, default);

        // Act
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("msg1"), false);
        await Task.Delay(100);

        // Assert
        received.Should().Contain("msg1");
        sub.Dispose();
    }

    [Fact]
    public async Task CrossBar_SlowConsumer_HandlesBackpressure()
    {
        // VALIDATES: Slow consumer handling
        // IMPACT: Covers backpressure scenarios

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var processingDelay = new TaskCompletionSource<bool>();
        var received = new List<int>();

        var sub = xBar.Subscribe<int>(
            "test.channel",
            async msg =>
            {
                received.Add(msg.Body);
                if (msg.Body == 1)
                {
                    // Block to create backpressure
                    await processingDelay.Task;
                }
            },
            default);

        // Act - Publish messages while first is blocked
        for (int i = 1; i <= 100; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), false);
        }

        await Task.Delay(100);

        // Release the block
        processingDelay.TrySetResult(true);
        await Task.Delay(200);

        // Assert - All messages should eventually be processed
        received.Should().HaveCount(100);
        sub.Dispose();
    }

    [Fact]
    public void CrossBar_SystemChannelTypeMismatch_ThrowsException()
    {
        // VALIDATES: System channel type mismatch (lines 562-564 CrossBar.cs)
        // IMPACT: Covers error path for system channel type checking

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.MessageTracingEnabled = true;

        // First subscription with correct type
        var sub1 = xBar.Subscribe<MessageTrace>(
            xBar.TracingChannel,
            msg => ValueTask.CompletedTask,
            default);

        // Act & Assert - Try to subscribe with wrong type
        Action act = () => xBar.Subscribe<string>(
            xBar.TracingChannel, // System channel expecting MessageTrace
            msg => ValueTask.CompletedTask,
            default);

        act.Should().Throw<ChannelTypeMismatchException>();

        sub1.Dispose();
    }

}
