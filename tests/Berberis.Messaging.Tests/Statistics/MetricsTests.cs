using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using Berberis.Messaging.Statistics;
using FluentAssertions;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Berberis.Messaging.Tests.Statistics;

/// <summary>
/// Tests for metrics export and serialization functionality.
/// These tests target the CrossBarExtensions.MetricsToJson method (83+ lines uncovered).
/// </summary>
public class MetricsTests
{
    [Fact]
    public async Task CrossBarExtensions_MetricsToJson_SerializesCorrectly()
    {
        // VALIDATES: MetricsToJson produces valid JSON with correct structure
        // VALIDATES: All metric types are serialized (channels, subscriptions, stats)
        // IMPACT: Covers 83+ lines in CrossBarExtensions.cs

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Create diverse activity to generate metrics
        // 1. Multiple channels
        var sub1 = xBar.Subscribe<string>("orders.new", msg => ValueTask.CompletedTask, default);
        var sub2 = xBar.Subscribe<int>("trades.executed", msg => ValueTask.CompletedTask, default);
        var sub3 = xBar.Subscribe<double>("prices.gold", msg => ValueTask.CompletedTask, default);

        // 2. Publish messages to generate statistics
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("orders.new", TestHelpers.CreateTestMessage($"order-{i}"), false);
            await xBar.Publish("trades.executed", TestHelpers.CreateTestMessage(i * 100), false);
            await xBar.Publish("prices.gold", TestHelpers.CreateTestMessage(1850.50 + i), false);
        }

        await Task.Delay(100); // Allow messages to be processed

        // Act - Call MetricsToJson
        var json = GetMetricsJson(xBar);

        // Assert - Validate JSON structure and content
        json.Should().NotBeNullOrEmpty("MetricsToJson should return non-empty JSON");

        // Parse JSON to validate structure
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Should contain channels section
        root.TryGetProperty("Channels", out var channels).Should().BeTrue();
        channels.GetArrayLength().Should().BeGreaterThanOrEqualTo(3, "should have at least 3 channels");

        // Validate channel metrics structure
        var firstChannel = channels[0];
        firstChannel.TryGetProperty("Channel", out _).Should().BeTrue("channel should have name");
        firstChannel.TryGetProperty("MessageBodyType", out _).Should().BeTrue("channel should have type");
        firstChannel.TryGetProperty("TotalMessages", out _).Should().BeTrue("channel should have message count");

        // Should contain subscriptions section
        root.TryGetProperty("Subscriptions", out var subscriptions).Should().BeTrue();
        subscriptions.GetArrayLength().Should().BeGreaterThanOrEqualTo(3);

        // Validate subscription metrics structure
        var firstSub = subscriptions[0];
        firstSub.TryGetProperty("Name", out _).Should().BeTrue("subscription should have name");
        firstSub.TryGetProperty("Subscriptions", out _).Should().BeTrue("subscription should have channel list");
        firstSub.TryGetProperty("TotalProcessedMessages", out _).Should().BeTrue();

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    [Fact]
    public void CrossBarExtensions_MetricsToJson_EmptyCrossBar_ReturnsValidJson()
    {
        // VALIDATES: MetricsToJson handles empty CrossBar (no channels/subscriptions)
        // VALIDATES: Edge case behavior

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Act
        var json = GetMetricsJson(xBar);

        // Assert
        json.Should().NotBeNullOrEmpty();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Should have structure even when empty
        root.TryGetProperty("Channels", out var channels).Should().BeTrue();
        channels.GetArrayLength().Should().Be(0);

        root.TryGetProperty("Subscriptions", out var subscriptions).Should().BeTrue();
        subscriptions.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CrossBarExtensions_MetricsToJson_WithStatistics_IncludesMetrics()
    {
        // VALIDATES: Statistics (latency, throughput) are included in JSON
        // VALIDATES: Floating point number formatting

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();

        // Subscribe with statistics enabled
        var sub = xBar.Subscribe<string>(
            "test.channel",
            msg => ValueTask.CompletedTask,
            subscriptionName: "test-sub",
            statsOptions: new StatsOptions(),
            token: default);

        // Publish messages to generate stats
        for (int i = 0; i < 50; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(200);

        // Act
        var json = GetMetricsJson(xBar);

        // Assert
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("Subscriptions", out var subscriptions).Should().BeTrue();
        subscriptions.GetArrayLength().Should().BeGreaterThan(0);

        var firstSub = subscriptions[0];

        // Should include statistical metrics
        firstSub.TryGetProperty("TotalProcessedMessages", out var msgCount).Should().BeTrue();
        msgCount.GetInt64().Should().BeGreaterThan(0);

        firstSub.TryGetProperty("ProcessRate", out _).Should().BeTrue();
        firstSub.TryGetProperty("AvgServiceTimeMs", out _).Should().BeTrue();

        sub.Dispose();
    }

    [Fact]
    public async Task CrossBarExtensions_MetricsToJson_UseMnemonics_UsesShortKeys()
    {
        // VALIDATES: useMnemonics parameter produces shortened JSON keys
        // VALIDATES: Both normal and mnemonic formats are valid

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var sub = xBar.Subscribe<string>("test.channel", msg => ValueTask.CompletedTask, default);

        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("test"), false);
        await Task.Delay(50);

        // Act - Generate JSON with mnemonics
        var jsonMnemonics = GetMetricsJson(xBar, useMnemonics: true);
        var jsonNormal = GetMetricsJson(xBar, useMnemonics: false);

        // Assert
        var docMnemonics = JsonDocument.Parse(jsonMnemonics);
        var docNormal = JsonDocument.Parse(jsonNormal);

        // Mnemonics should use short keys
        docMnemonics.RootElement.TryGetProperty("Chs", out _).Should().BeTrue("should use 'Chs' for channels");
        docMnemonics.RootElement.TryGetProperty("Sbs", out _).Should().BeTrue("should use 'Sbs' for subscriptions");

        // Normal should use full keys
        docNormal.RootElement.TryGetProperty("Channels", out _).Should().BeTrue("should use 'Channels'");
        docNormal.RootElement.TryGetProperty("Subscriptions", out _).Should().BeTrue("should use 'Subscriptions'");

        // Mnemonic JSON should be shorter
        jsonMnemonics.Length.Should().BeLessThan(jsonNormal.Length);

        sub.Dispose();
    }

    [Fact]
    public async Task CrossBarExtensions_MetricsToJson_HandlesFloatingPointEdgeCases()
    {
        // VALIDATES: NaN and Infinity values are handled correctly (written as null)
        // VALIDATES: Floating point rounding

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var sub = xBar.Subscribe<string>("test.channel", msg => ValueTask.CompletedTask, default);

        // Don't publish any messages - some stats will be NaN or 0
        await Task.Delay(50);

        // Act
        var json = GetMetricsJson(xBar);

        // Assert
        json.Should().NotBeNullOrEmpty();
        var doc = JsonDocument.Parse(json);

        // Should be valid JSON despite potential NaN/Infinity values
        doc.Should().NotBeNull();

        // The JSON should contain null values for NaN/Infinity instead of invalid JSON
        json.Should().NotContain("NaN");
        json.Should().NotContain("Infinity");

        sub.Dispose();
    }

    /// <summary>
    /// Helper method to serialize CrossBar metrics to JSON string
    /// </summary>
    private static string GetMetricsJson(CrossBar xBar, bool useMnemonics = false, bool resetStats = false)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream, new JsonWriterOptions { Indented = false });

        xBar.MetricsToJson(writer, useMnemonics, resetStats);
        writer.Flush();

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}
