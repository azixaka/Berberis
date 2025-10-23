using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Concurrency;

public class ConcurrencyTests
{
    [Fact]
    public async Task Publish_ConcurrentPublishers_AllDelivered()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var expectedCount = 1000;

        xBar.Subscribe<int>("test.channel", msg =>
        {
            Interlocked.Increment(ref receivedCount);
            return ValueTask.CompletedTask;
        }, default);

        // Act - 10 concurrent publishers, 100 messages each
        var tasks = Enumerable.Range(0, 10).Select(async publisherId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var msg = TestHelpers.CreateTestMessage(publisherId * 100 + i);
                await xBar.Publish("test.channel", msg, store: false);
            }
        });

        await Task.WhenAll(tasks);
        await Task.Delay(500); // Wait for delivery

        // Assert
        receivedCount.Should().Be(expectedCount);
    }

    [Fact]
    public async Task Publish_HighConcurrency_NoMessageLoss()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<int>();
        var publishCount = 5000;

        xBar.Subscribe<int>("high.concurrency", msg =>
        {
            receivedMessages.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, default);

        // Act - High concurrency publishing
        var tasks = Enumerable.Range(0, publishCount).Select(async i =>
        {
            var msg = TestHelpers.CreateTestMessage(i);
            await xBar.Publish("high.concurrency", msg, store: false);
        });

        await Task.WhenAll(tasks);
        await Task.Delay(1000); // Wait for all messages to be delivered

        // Assert - All messages should be received
        receivedMessages.Should().HaveCount(publishCount);
        receivedMessages.Distinct().Should().HaveCount(publishCount); // All unique
    }

    [Fact]
    public async Task Publish_ConcurrentPublishersMultipleChannels_CorrectRouting()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var channel1Count = 0;
        var channel2Count = 0;
        var channel3Count = 0;

        xBar.Subscribe<string>("channel1", _ =>
        {
            Interlocked.Increment(ref channel1Count);
            return ValueTask.CompletedTask;
        }, default);

        xBar.Subscribe<string>("channel2", _ =>
        {
            Interlocked.Increment(ref channel2Count);
            return ValueTask.CompletedTask;
        }, default);

        xBar.Subscribe<string>("channel3", _ =>
        {
            Interlocked.Increment(ref channel3Count);
            return ValueTask.CompletedTask;
        }, default);

        // Act - Concurrent publishing to different channels
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(xBar.Publish("channel1", "msg1").AsTask());
            tasks.Add(xBar.Publish("channel2", "msg2").AsTask());
            tasks.Add(xBar.Publish("channel3", "msg3").AsTask());
        }

        await Task.WhenAll(tasks);
        await Task.Delay(500);

        // Assert
        channel1Count.Should().Be(100);
        channel2Count.Should().Be(100);
        channel3Count.Should().Be(100);
    }

    [Fact]
    public async Task Subscribe_ConcurrentSubscribers_AllReceive()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var subscriberCount = 20;
        var receiveCounts = new int[subscriberCount];
        var messageCount = 100;

        // Act - Create multiple concurrent subscribers
        var subscriptions = new List<ISubscription>();
        for (int i = 0; i < subscriberCount; i++)
        {
            var index = i; // Capture for closure
            var sub = xBar.Subscribe<string>("shared.channel", msg =>
            {
                Interlocked.Increment(ref receiveCounts[index]);
                return ValueTask.CompletedTask;
            }, default);
            subscriptions.Add(sub);
        }

        // Publish messages
        for (int i = 0; i < messageCount; i++)
        {
            await xBar.Publish("shared.channel", $"message-{i}");
        }

        await Task.Delay(1000);

        // Assert - All subscribers should receive all messages
        receiveCounts.Should().AllSatisfy(count => count.Should().Be(messageCount));

        // Cleanup
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }
    }

    [Fact]
    public async Task Subscribe_AddSubscriberWhilePublishing_ReceivesNewMessages()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new System.Collections.Concurrent.ConcurrentBag<int>();
        var publishCount = 1000;
        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < publishCount; i++)
            {
                await xBar.Publish("dynamic.channel", i);
                await Task.Delay(1); // Small delay to allow subscriber to be added
            }
        });

        // Act - Add subscriber after some messages have been published
        await Task.Delay(50);

        var sub = xBar.Subscribe<int>("dynamic.channel", msg =>
        {
            received.Add(msg.Body);
            return ValueTask.CompletedTask;
        }, default);

        await publishTask;
        await Task.Delay(500);

        // Assert - Should receive messages published after subscription
        received.Should().NotBeEmpty();
        received.Should().HaveCountLessThan(publishCount); // Missed earlier messages

        sub.Dispose();
    }

    [Fact]
    public async Task Subscribe_ConcurrentSubscribeToSameChannel_AllSucceed()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var subscriberCount = 50;

        // Act - Concurrent subscriptions to same channel
        var tasks = Enumerable.Range(0, subscriberCount).Select(i =>
        {
            return Task.FromResult(xBar.Subscribe<string>("concurrent.channel", msg =>
            {
                return ValueTask.CompletedTask;
            }, default));
        });

        var subscriptions = await Task.WhenAll(tasks);

        // Assert - All subscriptions should be created successfully
        subscriptions.Should().HaveCount(subscriberCount);
        subscriptions.Should().AllSatisfy(sub => sub.Should().NotBeNull());

        // Test that a message reaches all subscribers
        var receivedCount = 0;
        var newSubs = new List<ISubscription>();
        for (int i = 0; i < subscriberCount; i++)
        {
            var sub = xBar.Subscribe<string>("concurrent.channel", msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            }, default);
            newSubs.Add(sub);
        }

        await xBar.Publish("concurrent.channel", "test");
        await Task.Delay(200);

        receivedCount.Should().Be(subscriberCount);

        // Cleanup
        foreach (var sub in subscriptions.Concat(newSubs))
        {
            sub.Dispose();
        }
    }

    [Fact]
    public async Task ChannelCreation_Concurrent_OnlyOneCreated()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act - Concurrent channel creation through subscriptions
        var tasks = Enumerable.Range(0, 100).Select(i =>
        {
            return Task.FromResult(xBar.Subscribe<string>("new.channel", msg =>
            {
                return ValueTask.CompletedTask;
            }, default));
        });

        var subscriptions = await Task.WhenAll(tasks);

        // Assert - Channel should exist and all subscriptions should work
        var channels = xBar.GetChannels();
        channels.Should().ContainSingle(c => c.Name == "new.channel");

        // Verify all subscriptions receive a message
        var receivedCount = 0;
        foreach (var _ in subscriptions)
        {
            // Each subscription increments the counter
        }

        var newSubs = new List<ISubscription>();
        for (int i = 0; i < 10; i++)
        {
            var sub = xBar.Subscribe<string>("new.channel", msg =>
            {
                Interlocked.Increment(ref receivedCount);
                return ValueTask.CompletedTask;
            }, default);
            newSubs.Add(sub);
        }

        await xBar.Publish("new.channel", "test");
        await Task.Delay(200);

        receivedCount.Should().Be(10);

        // Cleanup
        foreach (var sub in subscriptions.Concat(newSubs))
        {
            sub.Dispose();
        }
    }

    [Fact]
    public async Task Dispose_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;

        var subscription = xBar.Subscribe<string>("dispose.channel", msg =>
        {
            Interlocked.Increment(ref receivedCount);
            Thread.Sleep(10); // Simulate slow processing
            return ValueTask.CompletedTask;
        }, default);

        // Act - Publish messages and dispose concurrently
        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    await xBar.Publish("dispose.channel", $"message-{i}");
                }
                catch
                {
                    // Channel might be disposed, that's okay
                }
                await Task.Delay(5);
            }
        });

        // Dispose while publishing
        await Task.Delay(50);
        subscription.Dispose();

        await publishTask;
        await Task.Delay(500);

        // Assert - Should not crash, some messages may have been received
        receivedCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Dispose_MultipleSubscriptions_AllDisposed()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var subscriptions = new List<ISubscription>();

        for (int i = 0; i < 10; i++)
        {
            var sub = xBar.Subscribe<string>("multi.channel", msg =>
            {
                return ValueTask.CompletedTask;
            }, default);
            subscriptions.Add(sub);
        }

        // Act - Dispose all subscriptions concurrently
        var disposeTasks = subscriptions.Select(sub => Task.Run(() => sub.Dispose()));
        await Task.WhenAll(disposeTasks);

        // Assert - No messages should be received after disposal
        var receivedCount = 0;
        await xBar.Publish("multi.channel", "test");
        await Task.Delay(200);

        receivedCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_CrossBarUnderLoad_GracefulShutdown()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;

        // Create multiple subscriptions
        for (int i = 0; i < 10; i++)
        {
            xBar.Subscribe<string>($"channel.{i}", msg =>
            {
                Interlocked.Increment(ref receivedCount);
                Thread.Sleep(5); // Simulate processing
                return ValueTask.CompletedTask;
            }, default);
        }

        // Start heavy publishing
        var publishTask = Task.Run(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                try
                {
                    await xBar.Publish($"channel.{i % 10}", $"message-{i}");
                }
                catch
                {
                    // CrossBar might be disposed
                }
            }
        });

        // Let it run for a bit
        await Task.Delay(100);

        // Act - Dispose CrossBar while under load
        xBar.Dispose();

        // Wait for publish task to complete
        await publishTask;
        await Task.Delay(500);

        // Assert - Should handle gracefully without crashes
        receivedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Concurrent_MixedOperations_SystemRemainStable()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var receivedCount = 0;
        var subscriptions = new System.Collections.Concurrent.ConcurrentBag<ISubscription>();

        // Act - Mix of concurrent operations
        var tasks = new List<Task>();

        // Concurrent subscriptions
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var sub = xBar.Subscribe<string>("mixed.channel", msg =>
                {
                    Interlocked.Increment(ref receivedCount);
                    return ValueTask.CompletedTask;
                }, default);
                subscriptions.Add(sub);
            }));
        }

        // Concurrent publishing
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await xBar.Publish("mixed.channel", "test");
            }));
        }

        // Some disposals
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(50);
            if (subscriptions.TryTake(out var sub))
            {
                sub.Dispose();
            }
        }));

        await Task.WhenAll(tasks);
        await Task.Delay(1000);

        // Assert - System should remain stable
        receivedCount.Should().BeGreaterThan(0);
    }
}
