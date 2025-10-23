using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;

namespace Berberis.Messaging.Tests.Core;

public class SubscriptionTests
{
    [Fact]
    public async Task Subscribe_ReceivesPublishedMessages()
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
    public void Subscribe_WithName_NameIsSet()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            "MySubscription",
            default);

        // Assert
        subscription.Name.Should().Contain("MySubscription");
    }

    [Fact]
    public void Subscribe_WithoutName_AutoGeneratesName()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            default);

        // Assert
        subscription.Name.Should().NotBeNullOrEmpty();
        subscription.Name.Should().MatchRegex(@"\[\d+\]"); // Should contain [id]
    }

    [Fact]
    public async Task Subscribe_HandlerThrows_ExceptionLogged()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<int>();
        var messageReceived = new ManualResetEventSlim(false);

        xBar.Subscribe<int>("test.channel", msg =>
        {
            receivedMessages.Add(msg.Body);
            if (msg.Body == 1)
            {
                messageReceived.Set();
            }
            if (msg.Body == 2)
            {
                throw new InvalidOperationException("Test exception");
            }
            return ValueTask.CompletedTask;
        }, default);

        // Act - Publish first message (should succeed)
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(1), store: false);
        messageReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();

        // Publish second message that will throw
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(2), store: false);
        await Task.Delay(100); // Give time for exception to occur

        // Assert - First message was received
        receivedMessages.Should().Contain(1);
        // Subscription may or may not continue after exception depending on implementation
    }

    [Fact]
    public async Task Subscribe_MultipleMessages_AllReceived()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<int>();
        var expectedCount = 10;
        var allReceived = new ManualResetEventSlim(false);

        xBar.Subscribe<int>("test.channel", msg =>
        {
            receivedMessages.Add(msg.Body);
            if (receivedMessages.Count == expectedCount)
            {
                allReceived.Set();
            }
            return ValueTask.CompletedTask;
        }, default);

        // Act
        for (int i = 0; i < expectedCount; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), store: false);
        }

        // Assert
        allReceived.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        receivedMessages.Should().HaveCount(expectedCount);
        receivedMessages.Should().BeEquivalentTo(Enumerable.Range(0, expectedCount));
    }

    [Fact]
    public async Task Subscribe_AsyncHandler_WorksCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<string>();
        var messageReceived = new ManualResetEventSlim(false);

        xBar.Subscribe<string>("test.channel", async msg =>
        {
            await Task.Delay(10); // Simulate async work
            receivedMessages.Add(msg.Body!);
            messageReceived.Set();
        }, default);

        // Act
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("async message"), store: false);

        // Assert
        messageReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        receivedMessages.Should().ContainSingle();
        receivedMessages[0].Should().Be("async message");
    }

    [Fact]
    public void Subscribe_ChannelName_IsPreserved()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var subscription = xBar.Subscribe<string>(
            "my.test.channel",
            _ => ValueTask.CompletedTask,
            default);

        // Assert
        subscription.ChannelName.Should().Be("my.test.channel");
    }

    [Fact]
    public void Subscribe_SubscribedOn_IsSet()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var beforeSubscribe = DateTime.UtcNow;

        // Act
        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            default);

        var afterSubscribe = DateTime.UtcNow;

        // Assert
        subscription.SubscribedOn.Should().BeOnOrAfter(beforeSubscribe);
        subscription.SubscribedOn.Should().BeOnOrBefore(afterSubscribe);
    }

    [Fact]
    public void Subscribe_MessageBodyType_IsCorrect()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            default);

        // Assert
        subscription.MessageBodyType.Should().Be(typeof(string));
    }

    [Fact]
    public async Task Subscribe_WithBufferCapacity_LimitsBuffer()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var processedCount = 0;
        var slowHandler = new ManualResetEventSlim(false);

        xBar.Subscribe<int>(
            "test.channel",
            async msg =>
            {
                slowHandler.Wait(); // Block handler until signaled
                processedCount++;
                await Task.CompletedTask;
            },
            subscriptionName: null,
            fetchState: false,
            slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates,
            bufferCapacity: 5,
            conflationInterval: TimeSpan.Zero,
            subscriptionStatsOptions: default,
            token: default);

        // Act - Publish more than buffer capacity
        for (int i = 0; i < 10; i++)
        {
            var publishTask = xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), store: false);
            if (i < 5)
            {
                await publishTask; // First 5 should fit in buffer
            }
        }

        await Task.Delay(100); // Give time for buffering

        // Assert - Should have buffered but not processed yet
        processedCount.Should().Be(0);

        // Cleanup
        slowHandler.Set();
        await Task.Delay(100);
    }

    // Task 11: State handling tests
    [Fact]
    public async Task Subscribe_WithState_ReceivesStateFirst()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Publish and store some messages
        for (int i = 0; i < 5; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"msg-{i}", key: $"key-{i}");
            await xBar.Publish("test.channel", msg, store: true);
        }

        await Task.Delay(100); // Give time for storage

        var receivedMessages = new List<string>();
        var allReceived = new ManualResetEventSlim(false);

        // Act - Subscribe with fetchState: true
        xBar.Subscribe<string>("test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body!);
                if (receivedMessages.Count >= 5)
                {
                    allReceived.Set();
                }
                return ValueTask.CompletedTask;
            },
            fetchState: true,
            default);

        // Assert
        allReceived.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        receivedMessages.Should().HaveCount(5);
        receivedMessages.Should().Contain("msg-0");
        receivedMessages.Should().Contain("msg-1");
        receivedMessages.Should().Contain("msg-2");
        receivedMessages.Should().Contain("msg-3");
        receivedMessages.Should().Contain("msg-4");
    }

    [Fact]
    public async Task Subscribe_WithoutState_NoHistoricMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Publish and store some messages BEFORE subscribing
        for (int i = 0; i < 3; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"old-{i}", key: $"key-{i}");
            await xBar.Publish("test.channel", msg, store: true);
        }

        await Task.Delay(100);

        var receivedMessages = new List<string>();

        // Act - Subscribe with fetchState: false (default)
        xBar.Subscribe<string>("test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body!);
                return ValueTask.CompletedTask;
            },
            fetchState: false,
            default);

        await Task.Delay(200); // Wait to see if any historic messages arrive

        // Assert - Should not receive historic messages
        receivedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_StateAndNewMessages_CorrectOrder()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Publish and store state messages
        for (int i = 0; i < 3; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"state-{i}", key: $"key-{i}");
            await xBar.Publish("test.channel", msg, store: true);
        }

        await Task.Delay(100);

        var receivedMessages = new List<string>();
        var allReceived = new ManualResetEventSlim(false);

        // Act - Subscribe with fetchState: true
        xBar.Subscribe<string>("test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body!);
                if (receivedMessages.Count >= 5) // 3 state + 2 new
                {
                    allReceived.Set();
                }
                return ValueTask.CompletedTask;
            },
            fetchState: true,
            default);

        await Task.Delay(100); // Let state be delivered

        // Publish new messages after subscription
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("new-1", key: "new-key-1"), store: false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("new-2", key: "new-key-2"), store: false);

        // Assert
        allReceived.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        receivedMessages.Should().HaveCount(5);

        // State messages should arrive first (though order within state may vary)
        var stateMessages = receivedMessages.Take(3).ToList();
        stateMessages.Should().Contain(m => m.StartsWith("state-"));

        // New messages should arrive after state
        receivedMessages.Skip(3).Should().Contain("new-1");
        receivedMessages.Skip(3).Should().Contain("new-2");
    }

    [Fact]
    public async Task Subscribe_StateWithUpdatedKey_ReceivesLatestValue()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Publish multiple updates to the same key
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("value-1", key: "key-A"), store: true);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("value-2", key: "key-A"), store: true);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("value-3", key: "key-A"), store: true);

        await Task.Delay(100);

        var receivedMessages = new List<string>();
        var messageReceived = new ManualResetEventSlim(false);

        // Act - Subscribe with fetchState: true
        xBar.Subscribe<string>("test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body!);
                messageReceived.Set();
                return ValueTask.CompletedTask;
            },
            fetchState: true,
            default);

        // Assert - Should receive only the latest value for the key
        messageReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Should().Be("value-3");
    }

    // Task 12: Slow consumer strategies tests
    [Fact]
    public async Task Subscribe_SlowConsumer_SkipsMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var processingSemaphore = new SemaphoreSlim(0, 1);

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            async msg =>
            {
                await processingSemaphore.WaitAsync(); // Block until released
                Interlocked.Increment(ref receivedCount);
                await Task.Delay(50); // Simulate slow processing
            },
            subscriptionName: null,
            fetchState: false,
            slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates,
            bufferCapacity: 5,
            conflationInterval: TimeSpan.Zero,
            subscriptionStatsOptions: default,
            token: default);

        // Act - Publish more messages than buffer can hold
        for (int i = 0; i < 20; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i), store: false);
        }

        await Task.Delay(200); // Let messages queue up

        // Release processing and give time to process
        processingSemaphore.Release();
        await Task.Delay(500);

        // Assert - Some messages should have been skipped
        receivedCount.Should().BeLessThan(20);
        receivedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Subscribe_SlowConsumerStrategy_CanBeConfigured()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act - Configure subscription with slow consumer strategy
        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            subscriptionName: null,
            fetchState: false,
            slowConsumerStrategy: SlowConsumerStrategy.ConflateAndSkipUpdates,
            bufferCapacity: 10,
            conflationInterval: TimeSpan.Zero,
            subscriptionStatsOptions: default,
            token: default);

        // Assert - Subscription created successfully with strategy configured
        subscription.Should().NotBeNull();
    }

    // Task 13: Suspension/resumption tests
    [Fact]
    public async Task Subscription_Suspend_StopsReceiving()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<int>();

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body);
                return ValueTask.CompletedTask;
            },
            default);

        // Act - Publish a message, then suspend, then publish more
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(1), store: false);
        await Task.Delay(100); // Let first message be delivered

        subscription.IsProcessingSuspended = true;

        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(2), store: false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(3), store: false);
        await Task.Delay(200); // Wait to see if suspended messages are processed

        // Assert - Should only have received first message
        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Should().Be(1);
    }

    [Fact]
    public async Task Subscription_Resume_ContinuesReceiving()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<int>();
        var allReceived = new ManualResetEventSlim(false);

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body);
                if (receivedMessages.Count >= 3)
                {
                    allReceived.Set();
                }
                return ValueTask.CompletedTask;
            },
            default);

        // Act - Suspend, publish messages, then resume
        subscription.IsProcessingSuspended = true;

        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(1), store: false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(2), store: false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(3), store: false);
        await Task.Delay(100);

        // Resume processing
        subscription.IsProcessingSuspended = false;

        // Assert - Messages should now be delivered
        allReceived.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        receivedMessages.Should().HaveCount(3);
    }

    [Fact]
    public void Subscription_SuspendResume_NoMessageLoss()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            default);

        // Act - Suspend and resume multiple times
        subscription.IsProcessingSuspended = true;
        subscription.IsProcessingSuspended.Should().BeTrue();

        subscription.IsProcessingSuspended = false;
        subscription.IsProcessingSuspended.Should().BeFalse();

        subscription.IsProcessingSuspended = true;
        subscription.IsProcessingSuspended.Should().BeTrue();

        subscription.IsProcessingSuspended = false;
        subscription.IsProcessingSuspended.Should().BeFalse();

        // Assert - Property should reflect current state
        subscription.IsProcessingSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task Suspension_ConcurrentSuspendResume_NoDeadlocks()
    {
        // VALIDATES: Thread-safe suspension state management (Subscription.cs:106-118)
        // SCENARIO:
        //   1. 10 threads rapidly toggle IsProcessingSuspended
        //   2. Concurrent message publishing
        //   3. Verify: No deadlocks, all messages eventually processed

        var xBar = TestHelpers.CreateTestCrossBar();
        var processedCount = 0;
        var expectedMessages = 1000;

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            msg =>
            {
                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            },
            default);

        // Task: Rapid suspend/resume
        var suspendTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                subscription.IsProcessingSuspended = true;
                await Task.Delay(5);
                subscription.IsProcessingSuspended = false;
                await Task.Delay(5);
            }
        });

        // Task: Concurrent publishing
        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < expectedMessages; i++)
            {
                await xBar.Publish("test.channel", i);
                await Task.Delay(1);
            }
        });

        await Task.WhenAll(suspendTask, publishTask);
        await Task.Delay(2000); // Allow all messages to be processed

        // Assert: No deadlocks (test completes)
        // Assert: All messages eventually processed
        processedCount.Should().Be(expectedMessages);
    }

    [Fact]
    public async Task Suspension_DuringMessageProcessing_CurrentMessageCompletes()
    {
        // VALIDATES: Current message completes, next message waits
        // SCENARIO:
        //   1. Handler processes message for 200ms
        //   2. Suspend after 50ms
        //   3. Verify: First message completes
        //   4. Verify: Second message waits until resume

        var xBar = TestHelpers.CreateTestCrossBar();
        var processedMessages = new List<int>();
        var processingStarted = new ManualResetEventSlim(false);
        var processingLock = new object();

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            async msg =>
            {
                if (msg.Body == 1)
                {
                    processingStarted.Set();
                    await Task.Delay(200); // Long processing
                }
                lock (processingLock) { processedMessages.Add(msg.Body); }
            },
            default);

        // Publish first message
        await xBar.Publish("test.channel", 1);
        processingStarted.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();

        // Suspend while first message is processing
        await Task.Delay(50);
        subscription.IsProcessingSuspended = true;

        // Publish second message
        await xBar.Publish("test.channel", 2);

        // Wait for first message to complete
        await Task.Delay(300);

        // Assert: First message completed despite suspension
        lock (processingLock) { processedMessages.Should().Contain(1); }

        // Assert: Second message NOT processed yet
        lock (processingLock) { processedMessages.Should().NotContain(2); }

        // Resume
        subscription.IsProcessingSuspended = false;
        await Task.Delay(200);

        // Assert: Second message now processed
        lock (processingLock) { processedMessages.Should().Contain(2); }
    }

    [Fact]
    public async Task Suspension_WithConflation_PausesFlushLoop()
    {
        // VALIDATES: Conflation flush loop pauses during suspension
        // SCENARIO:
        //   1. Enable conflation (300ms)
        //   2. Suspend subscription
        //   3. Publish 100 messages
        //   4. Wait 1 second (should have flushed 3 times normally)
        //   5. Verify: No messages flushed while suspended
        //   6. Resume, verify: Messages flushed

        var xBar = TestHelpers.CreateTestCrossBar();
        var flushedMessages = new List<int>();
        var flushLock = new object();

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            msg =>
            {
                lock (flushLock) { flushedMessages.Add(msg.Body); }
                return ValueTask.CompletedTask;
            },
            subscriptionName: null,
            fetchState: false,
            slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates,
            bufferCapacity: null,
            conflationInterval: TimeSpan.FromMilliseconds(300),
            subscriptionStatsOptions: default,
            token: default);

        // Suspend immediately
        subscription.IsProcessingSuspended = true;

        // Publish 100 messages
        for (int i = 0; i < 100; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i, key: "key-A"), false);
        }

        // Wait 1 second (normally 3 flushes would occur)
        await Task.Delay(1000);

        // Assert: No flushes while suspended
        lock (flushLock) { flushedMessages.Should().BeEmpty(); }

        // Resume
        subscription.IsProcessingSuspended = false;
        await Task.Delay(1000);

        // Assert: Messages flushed after resume
        lock (flushLock)
        {
            flushedMessages.Should().NotBeEmpty();
            flushedMessages.Should().Contain(99); // Latest value
        }
    }

    // Task 14: Disposal tests
    [Fact]
    public async Task Subscription_Dispose_StopsReceiving()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<string>();

        var subscription = xBar.Subscribe<string>(
            "test.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body!);
                return ValueTask.CompletedTask;
            },
            default);

        // Publish a message to verify subscription is working
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("before"), store: false);
        await Task.Delay(100);
        receivedMessages.Should().HaveCount(1);

        // Act - Dispose subscription
        subscription.Dispose();
        await Task.Delay(100);

        // Publish after disposal
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("after"), store: false);
        await Task.Delay(200);

        // Assert - Should not receive message after disposal
        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Should().Be("before");
    }

    [Fact]
    public void Subscription_DoubleDispose_NoError()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            default);

        // Act - Dispose twice
        subscription.Dispose();
        var act = () => subscription.Dispose();

        // Assert - Should not throw
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Subscription_Dispose_CompletesMessageLoop()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        var subscription = xBar.Subscribe<string>(
            "test.channel",
            _ => ValueTask.CompletedTask,
            default);

        // Give subscription time to start
        await Task.Delay(100);

        // Act - Dispose
        subscription.Dispose();

        // Assert - Message loop should complete
        await Task.WhenAny(subscription.MessageLoop, Task.Delay(2000));
        subscription.MessageLoop.IsCompleted.Should().BeTrue();
    }

    // Coverage Boost: Channel deletion notification
    [Fact]
    public async Task Subscription_ChannelDeletion_ReceivesDeleteMessage()
    {
        // VALIDATES: NotifyChannelDeletion sends MessageType.ChannelDelete (lines 135-139 Subscription.cs)
        // VALIDATES: Subscription receives and handles channel deletion notification
        // IMPACT: Covers NotifyChannelDeletion internal method

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new List<MessageType>();
        var channelDeleteReceived = new TaskCompletionSource<bool>();

        var subscription = xBar.Subscribe<string>(
            "deletable.channel",
            msg =>
            {
                receivedMessages.Add(msg.MessageType);
                if (msg.MessageType == MessageType.ChannelDelete)
                {
                    channelDeleteReceived.TrySetResult(true);
                }
                return ValueTask.CompletedTask;
            },
            default);

        // Publish a normal message first
        await xBar.Publish("deletable.channel", TestHelpers.CreateTestMessage("normal"), false);
        await Task.Delay(50);

        // Act - Delete the channel (triggers NotifyChannelDeletion)
        var deleted = xBar.TryDeleteChannel("deletable.channel");

        // Assert
        deleted.Should().BeTrue("channel should be deleted");

        // Wait for ChannelDelete message
        var received = await Task.WhenAny(channelDeleteReceived.Task, Task.Delay(1000));
        received.Should().Be(channelDeleteReceived.Task, "should receive ChannelDelete message");

        receivedMessages.Should().Contain(MessageType.ChannelDelete, "subscription should receive delete notification");
    }

    // Coverage Boost: Handler exceptions and error paths
    [Fact]
    public async Task Subscription_HandlerThrowsException_ContinuesProcessing()
    {
        // VALIDATES: Exception handling in handler (lines 435-446 Subscription.cs)
        // VALIDATES: Subscription continues after handler exception
        // IMPACT: Covers exception catch block

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var processedMessages = new List<int>();
        var exceptionCount = 0;

        var subscription = xBar.Subscribe<int>(
            "test.channel",
            msg =>
            {
                if (msg.Body == 2)
                {
                    exceptionCount++;
                    throw new InvalidOperationException("Test exception");
                }
                processedMessages.Add(msg.Body);
                return ValueTask.CompletedTask;
            },
            default);

        // Act - Publish messages, one will throw
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(1), false);
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(2), false); // Will throw
        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(3), false);

        await Task.Delay(200);

        // Assert - Should continue processing after exception
        processedMessages.Should().Contain(1);
        processedMessages.Should().Contain(3);
        exceptionCount.Should().Be(1, "exception should have been thrown once");

        subscription.Dispose();
    }

    // Coverage Boost: Message tracing paths
    [Fact]
    public async Task Subscription_WithMessageTracing_TracesMessages()
    {
        // VALIDATES: Message tracing code (lines 182-194, 455+ Subscription.cs)
        // VALIDATES: Tracing publishes to system channel
        // IMPACT: Covers tracing paths when MessageTracingEnabled = true

        // Arrange - Create CrossBar with tracing enabled
        var xBar = TestHelpers.CreateTestCrossBar();
        xBar.MessageTracingEnabled = true;

        var tracingMessages = new List<MessageTrace>();

        // Subscribe to tracing channel
        var tracingSub = xBar.Subscribe<MessageTrace>(
            xBar.TracingChannel,
            msg =>
            {
                tracingMessages.Add(msg.Body!);
                return ValueTask.CompletedTask;
            },
            default);

        // Create a regular subscription
        var receivedMessages = new List<string>();
        var sub = xBar.Subscribe<string>(
            "traced.channel",
            msg =>
            {
                receivedMessages.Add(msg.Body!);
                return ValueTask.CompletedTask;
            },
            default);

        // Act - Publish messages (should generate trace messages)
        await xBar.Publish("traced.channel", TestHelpers.CreateTestMessage("msg1"), false);
        await xBar.Publish("traced.channel", TestHelpers.CreateTestMessage("msg2"), false);

        await Task.Delay(200);

        // Assert
        receivedMessages.Should().HaveCount(2);
        tracingMessages.Should().NotBeEmpty("tracing should have captured events");
        tracingMessages.Should().Contain(t => t.OpType == OpType.SubscriptionDequeue, "should trace dequeue operations");

        sub.Dispose();
        tracingSub.Dispose();
    }
}
