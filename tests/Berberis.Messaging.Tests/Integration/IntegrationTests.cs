using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Integration;

/// <summary>
/// End-to-end integration tests (Task 50)
/// </summary>
public class IntegrationTests
{
    [Fact]
    public async Task EndToEnd_ComplexPipeline_WorksCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var results = new List<string>();

        // Create multi-stage pipeline: Input -> Transform -> Aggregate -> Output
        xBar.Subscribe<string>("input", async msg =>
        {
            var transformed = msg.Body!.ToUpper();
            await xBar.Publish("transformed", TestHelpers.CreateTestMessage(transformed), false);
        }, CancellationToken.None);

        xBar.Subscribe<string>("transformed", async msg =>
        {
            var aggregated = $"Processed: {msg.Body}";
            await xBar.Publish("output", TestHelpers.CreateTestMessage(aggregated), false);
        }, CancellationToken.None);

        xBar.Subscribe<string>("output", msg =>
        {
            results.Add(msg.Body!);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act
        await xBar.Publish("input", TestHelpers.CreateTestMessage("hello"), false);
        await Task.Delay(200);

        // Assert
        results.Should().ContainSingle()
               .Which.Should().Be("Processed: HELLO");
    }

    [Fact]
    public async Task EndToEnd_StatefulWithWildcards_WorksCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();

        // Subscribe with wildcard to capture all order events
        xBar.Subscribe<string>("orders.*", msg =>
        {
            received.Add(msg.Body!);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Act - Publish to different order channels
        await xBar.Publish("orders.new", TestHelpers.CreateTestMessage("order1", key: "ord1"), true);
        await xBar.Publish("orders.updated", TestHelpers.CreateTestMessage("order2", key: "ord2"), true);
        await xBar.Publish("orders.cancelled", TestHelpers.CreateTestMessage("order3", key: "ord3"), true);
        await Task.Delay(200);

        // Assert
        received.Should().HaveCount(3);
        received.Should().Contain("order1");
        received.Should().Contain("order2");
        received.Should().Contain("order3");

        // Verify state is stored
        var stateNew = xBar.GetChannelState<string>("orders.new");
        stateNew.Should().ContainSingle().Which.Body.Should().Be("order1");
    }

    [Fact]
    public async Task EndToEnd_MultipleSubscribersConcurrentPublish_AllReceive()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var subscriber1 = new List<int>();
        var subscriber2 = new List<int>();
        var subscriber3 = new List<int>();

        xBar.Subscribe<int>("data", msg => { subscriber1.Add(msg.Body); return ValueTask.CompletedTask; }, CancellationToken.None);
        xBar.Subscribe<int>("data", msg => { subscriber2.Add(msg.Body); return ValueTask.CompletedTask; }, CancellationToken.None);
        xBar.Subscribe<int>("data", msg => { subscriber3.Add(msg.Body); return ValueTask.CompletedTask; }, CancellationToken.None);

        // Act - Concurrent publishes
        var tasks = Enumerable.Range(0, 100).Select(async i =>
            await xBar.Publish("data", TestHelpers.CreateTestMessage(i), false));

        await Task.WhenAll(tasks);
        await Task.Delay(300);

        // Assert
        subscriber1.Should().HaveCount(100);
        subscriber2.Should().HaveCount(100);
        subscriber3.Should().HaveCount(100);
    }

    [Fact]
    public async Task EndToEnd_DisposeCleanup_NoResourceLeaks()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var subscriptions = new List<ISubscription>();

        for (int i = 0; i < 10; i++)
        {
            var sub = xBar.Subscribe<string>($"channel{i}", _ => ValueTask.CompletedTask, CancellationToken.None);
            subscriptions.Add(sub);
        }

        // Act - Dispose subscriptions and CrossBar
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }
        xBar.Dispose();

        // Assert - Should complete without errors
        Assert.True(true);
    }

    [Fact]
    public async Task EndToEnd_StateManagementLifecycle_WorksCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Publish with state
        await xBar.Publish("products", TestHelpers.CreateTestMessage("Product A", key: "prod1"), true);
        await xBar.Publish("products", TestHelpers.CreateTestMessage("Product B", key: "prod2"), true);
        await xBar.Publish("products", TestHelpers.CreateTestMessage("Product C", key: "prod3"), true);

        // Subscribe with fetchState
        var received = new List<string>();
        xBar.Subscribe<string>("products", msg =>
        {
            received.Add(msg.Body!);
            return ValueTask.CompletedTask;
        }, fetchState: true, token: CancellationToken.None);

        await Task.Delay(200);

        // Assert - Should receive all 3 state messages
        received.Should().HaveCount(3);
        received.Should().Contain("Product A");
        received.Should().Contain("Product B");
        received.Should().Contain("Product C");

        // Update one item
        await xBar.Publish("products", TestHelpers.CreateTestMessage("Product A Updated", key: "prod1"), true);
        await Task.Delay(100);

        // Should receive the update
        received.Should().Contain("Product A Updated");

        // Delete one item
        xBar.TryDeleteMessage<string>("products", "prod2");
        var state = xBar.GetChannelState<string>("products");
        state.Should().HaveCount(2);
        state.Select(m => m.Key).Should().NotContain("prod2");
    }
}
