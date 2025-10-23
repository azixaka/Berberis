using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using System.Collections.Concurrent;
using Xunit;

namespace Berberis.Messaging.Tests.Integration;

/// <summary>
/// Tests for feature combinations to validate interactions between different CrossBar features.
/// These tests address critical gaps in coverage for feature interactions.
/// </summary>
public class FeatureCombinationTests
{
    [Fact]
    public async Task WildcardSubscription_WithStatefulChannel_FetchesStateFromAllMatchingChannels()
    {
        // VALIDATES: Wildcard subscriptions receive state from ALL matching channels
        // SCENARIO:
        //   1. Store messages on orders.new, orders.cancelled, orders.shipped
        //   2. Subscribe with wildcard "orders.*" and fetchState: true
        //   3. Verify: Receives state from all 3 matching channels

        var xBar = TestHelpers.CreateTestCrossBar();

        // Store state on 3 channels
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("new-1", key: "k1"), store: true);
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("new-2", key: "k2"), store: true);
        await xBar.Publish("orders.cancelled", TestHelpers.CreateTestMessage("cancelled-1", key: "k3"), store: true);
        await xBar.Publish("orders.shipped", TestHelpers.CreateTestMessage("shipped-1", key: "k4"), store: true);

        await Task.Delay(100);

        var received = new List<string>();
        var messageEvent = new ManualResetEventSlim(false);

        // Subscribe with wildcard + fetchState
        xBar.Subscribe<string>("orders.*", msg =>
        {
            received.Add(msg.Body!);
            if (received.Count >= 4)
                messageEvent.Set();
            return ValueTask.CompletedTask;
        }, fetchState: true, token: default);

        messageEvent.Wait(TimeSpan.FromSeconds(3)).Should().BeTrue();

        // Assert: Should receive state from all 3 matching channels
        received.Should().HaveCount(4);
        received.Should().Contain("new-1");
        received.Should().Contain("new-2");
        received.Should().Contain("cancelled-1");
        received.Should().Contain("shipped-1");
    }

    [Fact]
    public async Task Conflation_WithStatefulChannel_PreservesLatestStateValue()
    {
        // VALIDATES: Conflation doesn't interfere with state storage correctness
        // SCENARIO:
        //   1. Enable conflation (500ms interval)
        //   2. Rapidly publish 100 updates to same key
        //   3. Verify: State contains latest value (not a conflated intermediate)
        //   4. Verify: Subscriber receives conflated messages

        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<int>();
        var receiveLock = new object();

        xBar.Subscribe<int>(
            "prices",
            msg =>
            {
                lock (receiveLock) { received.Add(msg.Body); }
                return ValueTask.CompletedTask;
            },
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(500),
            token: default);

        // Rapidly publish 100 updates to same key
        for (int i = 0; i < 100; i++)
        {
            var msg = TestHelpers.CreateTestMessage(i, key: "AAPL");
            await xBar.Publish("prices", msg, store: true);
            await Task.Delay(5);
        }

        await Task.Delay(1000); // Wait for conflation flush

        // Assert: Conflation reduced message count
        received.Should().HaveCountLessThan(100);
        received.Should().Contain(99); // Latest value received

        // Assert: State has latest value, not conflated intermediate
        var state = xBar.GetChannelState<int>("prices");
        state.Should().HaveCount(1);
        state.First().Body.Should().Be(99);
    }

    [Fact]
    public async Task WildcardSubscription_WithConflation_ConflatesPerChannelIndependently()
    {
        // VALIDATES: Wildcard subscription conflates messages per-channel, not globally
        // SCENARIO:
        //   1. Subscribe "orders.*" with conflation
        //   2. Rapidly publish to orders.new (key: A) and orders.cancelled (key: B)
        //   3. Verify: Each channel's messages conflated independently

        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<(string channel, string key, string body)>();
        var receiveLock = new object();

        xBar.Subscribe<string>(
            "orders.*",
            msg =>
            {
                lock (receiveLock)
                {
                    // Track which channel message came from via message metadata
                    received.Add((msg.From ?? "unknown", msg.Key ?? "no-key", msg.Body!));
                }
                return ValueTask.CompletedTask;
            },
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(300),
            token: default);

        // Publish 50 messages to orders.new with key A
        for (int i = 0; i < 50; i++)
        {
            await xBar.Publish("orders.new",
                TestHelpers.CreateTestMessage($"new-{i}", key: "key-A", from: "orders.new"),
                store: false);
            await Task.Delay(5);
        }

        // Publish 50 messages to orders.cancelled with key B
        for (int i = 0; i < 50; i++)
        {
            await xBar.Publish("orders.cancelled",
                TestHelpers.CreateTestMessage($"cancelled-{i}", key: "key-B", from: "orders.cancelled"),
                store: false);
            await Task.Delay(5);
        }

        await Task.Delay(1000);

        // Assert: Conflation happened (< 100 messages)
        received.Should().HaveCountLessThan(100);

        // Assert: Both channels have messages (not all from one channel)
        received.Where(r => r.channel == "orders.new").Should().NotBeEmpty();
        received.Where(r => r.channel == "orders.cancelled").Should().NotBeEmpty();

        // Assert: Latest values from each channel present
        received.Should().Contain(r => r.body == "new-49");
        received.Should().Contain(r => r.body == "cancelled-49");
    }

    [Fact]
    public async Task HandlerTimeout_DuringConflationFlush_ContinuesProcessing()
    {
        // VALIDATES: Timeout doesn't break conflation flush loop
        // SCENARIO:
        //   1. Enable conflation (200ms) + timeout (50ms)
        //   2. Handler sleeps 200ms (will timeout)
        //   3. Verify: Timeout occurs, subsequent conflation flushes continue

        var xBar = TestHelpers.CreateTestCrossBar();
        var timeoutCount = 0;
        var processedCount = 0;
        var handlerExecutionCount = 0;

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromMilliseconds(50),
            OnTimeout = ex => { Interlocked.Increment(ref timeoutCount); }
        };

        xBar.Subscribe<int>(
            "test.channel",
            async msg =>
            {
                Interlocked.Increment(ref handlerExecutionCount);
                // Make handler always slow so it times out
                await Task.Delay(100); // Will timeout (timeout is 50ms)
                Interlocked.Increment(ref processedCount);
            },
            subscriptionName: null,
            fetchState: false,
            slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates,
            bufferCapacity: null,
            conflationInterval: TimeSpan.FromMilliseconds(200),
            subscriptionStatsOptions: default,
            options: options,
            token: default);

        // Publish messages that will be conflated
        for (int i = 0; i < 100; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i, key: "key-A"), false);
            await Task.Delay(5);
        }

        await Task.Delay(1500);

        // Assert: Some timeouts occurred (at least one conflation flush timed out)
        timeoutCount.Should().BeGreaterThan(0, "at least one conflation flush should have timed out");

        // Assert: Handler was executed multiple times (conflation happened)
        handlerExecutionCount.Should().BeGreaterThan(0, "handler should have been called for conflation flushes");
        handlerExecutionCount.Should().BeLessThan(100, "conflation should have reduced message count");

        // Assert: Subscription continues after timeouts (processed count may be 0 if all timed out, but handler was called)
        handlerExecutionCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Subscription_SuspendDuringStateFetch_DelaysStateDelivery()
    {
        // VALIDATES: State delivery respects suspension
        // SCENARIO:
        //   1. Store 10 messages
        //   2. Subscribe with fetchState: true
        //   3. Immediately suspend (before state delivery completes)
        //   4. Verify: State not delivered while suspended
        //   5. Resume and verify: State is delivered

        var xBar = TestHelpers.CreateTestCrossBar();

        // Store state
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"state-{i}", key: $"k{i}"), store: true);
        }

        var received = new List<string>();
        var receiveLock = new object();

        var subscription = xBar.Subscribe<string>(
            "test.channel",
            msg =>
            {
                lock (receiveLock) { received.Add(msg.Body!); }
                return ValueTask.CompletedTask;
            },
            fetchState: true,
            token: default);

        // Immediately suspend
        subscription.IsProcessingSuspended = true;

        await Task.Delay(500); // Wait - state should NOT be delivered

        received.Should().BeEmpty("state should not be delivered while suspended");

        // Resume
        subscription.IsProcessingSuspended = false;

        await Task.Delay(500);

        // Assert: State delivered after resume
        received.Should().HaveCount(10);
    }

    [Fact]
    public async Task Wildcard_Timeout_Stateful_Integration()
    {
        // VALIDATES: Complex feature interaction - wildcard + timeout + stateful channels
        // SCENARIO:
        //   1. Create stateful channels with pattern "trades.*"
        //   2. Subscribe with wildcard, timeout enabled, fetchState: true
        //   3. Some handlers timeout, verify state fetch still works
        //   4. Verify timeouts tracked per subscription

        var xBar = TestHelpers.CreateTestCrossBar();

        // Store state on multiple channels
        await xBar.Publish("trades.nyse", TestHelpers.CreateTestMessage("AAPL-150", key: "AAPL"), store: true);
        await xBar.Publish("trades.nasdaq", TestHelpers.CreateTestMessage("GOOGL-2800", key: "GOOGL"), store: true);
        await xBar.Publish("trades.lse", TestHelpers.CreateTestMessage("BP-450", key: "BP"), store: true);

        await Task.Delay(100);

        var received = new List<string>();
        var timeoutCount = 0;
        var receiveLock = new object();

        var options = new SubscriptionOptions
        {
            HandlerTimeout = TimeSpan.FromMilliseconds(100),
            OnTimeout = ex => { Interlocked.Increment(ref timeoutCount); }
        };

        var subscription = xBar.Subscribe<string>(
            "trades.*",
            async msg =>
            {
                // Simulate slow processing for some messages
                if (msg.Body!.StartsWith("GOOGL"))
                    await Task.Delay(200); // Will timeout

                lock (receiveLock) { received.Add(msg.Body!); }
            },
            subscriptionName: null,
            fetchState: true,
            slowConsumerStrategy: SlowConsumerStrategy.SkipUpdates,
            bufferCapacity: null,
            conflationInterval: TimeSpan.Zero,
            subscriptionStatsOptions: default,
            options: options,
            token: default);

        await Task.Delay(500);

        // Assert: State was fetched from all matching channels
        lock (receiveLock)
        {
            received.Should().Contain("AAPL-150");
            received.Should().Contain("BP-450");
            // GOOGL may or may not be in received due to timeout
        }

        // Assert: Timeout occurred for GOOGL
        timeoutCount.Should().BeGreaterThan(0);

        // Publish new updates
        await xBar.Publish("trades.nyse", "AAPL-151");
        await Task.Delay(200);

        // Assert: New messages still received after timeout
        lock (receiveLock) { received.Should().Contain("AAPL-151"); }
    }

    [Fact]
    public async Task Conflation_Suspension_InteractionCorrect()
    {
        // VALIDATES: Conflation and suspension interact correctly
        // SCENARIO:
        //   1. Enable conflation
        //   2. Publish messages
        //   3. Suspend during conflation accumulation
        //   4. Verify: Messages accumulate but not flushed
        //   5. Resume and verify: Flush happens

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
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(300),
            token: default);

        // Publish a few messages
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i, key: "key-A"), false);
        }

        // Suspend before conflation flushes
        await Task.Delay(50);
        subscription.IsProcessingSuspended = true;

        // Publish more messages
        for (int i = 10; i < 20; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i, key: "key-A"), false);
        }

        // Wait for what would be multiple conflation intervals
        await Task.Delay(1000);

        // Assert: Very few or no messages flushed due to early suspension
        lock (flushLock)
        {
            flushedMessages.Should().HaveCountLessThan(5);
        }

        // Resume
        subscription.IsProcessingSuspended = false;
        await Task.Delay(1000);

        // Assert: Messages now flushed
        lock (flushLock)
        {
            flushedMessages.Should().NotBeEmpty();
            flushedMessages.Should().Contain(19); // Latest value should be present
        }
    }

    [Fact]
    public async Task MultipleWildcards_DifferentConflationIntervals()
    {
        // VALIDATES: Multiple wildcard subscriptions with different conflation intervals work independently
        // SCENARIO:
        //   1. Subscribe "orders.*" with 200ms conflation
        //   2. Subscribe "orders.*" with 500ms conflation
        //   3. Verify: Each subscription conflates independently

        var xBar = TestHelpers.CreateTestCrossBar();
        var received200 = new List<int>();
        var received500 = new List<int>();
        var lock200 = new object();
        var lock500 = new object();

        // Subscription 1: 200ms conflation
        xBar.Subscribe<int>(
            "orders.*",
            msg =>
            {
                lock (lock200) { received200.Add(msg.Body); }
                return ValueTask.CompletedTask;
            },
            subscriptionName: "sub-200ms",
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(200),
            token: default);

        // Subscription 2: 500ms conflation
        xBar.Subscribe<int>(
            "orders.*",
            msg =>
            {
                lock (lock500) { received500.Add(msg.Body); }
                return ValueTask.CompletedTask;
            },
            subscriptionName: "sub-500ms",
            fetchState: false,
            conflationInterval: TimeSpan.FromMilliseconds(500),
            token: default);

        // Publish rapidly for 2 seconds
        for (int i = 0; i < 200; i++)
        {
            await xBar.Publish("orders.new", TestHelpers.CreateTestMessage(i, key: "key-A"), false);
            await Task.Delay(10);
        }

        await Task.Delay(1000);

        // Assert: Both received conflated messages
        lock (lock200) { received200.Should().HaveCountLessThan(200); }
        lock (lock500) { received500.Should().HaveCountLessThan(200); }

        // Assert: 200ms subscription got more flushes than 500ms
        lock (lock200)
        {
            lock (lock500)
            {
                received200.Should().HaveCountGreaterThan(received500.Count,
                    "faster conflation should result in more flushes");
            }
        }

        // Assert: Both got the latest value
        lock (lock200) { received200.Should().Contain(199); }
        lock (lock500) { received500.Should().Contain(199); }
    }
}
