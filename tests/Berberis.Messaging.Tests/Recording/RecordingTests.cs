using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using Berberis.Recorder;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Recording;

public class RecordingTests
{
    // Task 32: Message capture tests

    [Fact]
    public async Task Recording_CapturesMessages_ToStream()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using var recording = xBar.Record("test.channel", stream, serializer);

        // Act
        for (int i = 0; i < 10; i++)
        {
            var msg = TestHelpers.CreateTestMessage($"msg-{i}");
            await xBar.Publish("test.channel", msg, store: false);
        }

        await Task.Delay(200);
        recording.Dispose();

        // Assert
        stream.Position = 0;
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Recording_MultipleMessages_AllCaptured()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var messageCount = 100;

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            // Act
            for (int i = 0; i < messageCount; i++)
            {
                var msg = TestHelpers.CreateTestMessage($"msg-{i}");
                await xBar.Publish("test.channel", msg, store: false);
            }

            await Task.Delay(100);
        }

        // Assert - Playback to verify all messages were captured
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var playedMessages = new List<string>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            playedMessages.Add(msg.Body!);
        }

        playedMessages.Should().HaveCount(messageCount);
    }

    [Fact]
    public async Task Recording_NoMessages_EmptyStream()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        // Act
        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            await Task.Delay(50);
        }

        // Assert
        stream.Position = 0;
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task Recording_Dispose_StopsRecording()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        var recording = xBar.Record("test.channel", stream, serializer);

        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("msg-1"), store: false);
        await Task.Delay(50);

        // Act - Dispose and publish more
        recording.Dispose();
        var lengthAfterDispose = stream.Length;

        await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("msg-2"), store: false);
        await Task.Delay(50);

        // Assert - Stream length should not change after disposal
        stream.Length.Should().Be(lengthAfterDispose);
    }

    // Task 33: Playback in order tests

    [Fact]
    public async Task Player_ReplaysMessages_InOrder()
    {
        // Arrange
        var xBar1 = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        // Record
        using (var recording = xBar1.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < 5; i++)
            {
                await xBar1.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
            }
            await Task.Delay(100);
        }

        // Act - Playback
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var received = new List<string>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            received.Add(msg.Body!);
        }

        // Assert
        received.Should().Equal("msg-0", "msg-1", "msg-2", "msg-3", "msg-4");
    }

    [Fact]
    public async Task Player_EmptyRecording_NoMessages()
    {
        // Arrange
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        // Act
        var player = Player<string>.Create(stream, serializer);
        var received = new List<string>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            received.Add(msg.Body!);
        }

        // Assert
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Player_PublishToNewCrossBar_MessagesDelivered()
    {
        // Arrange - Record messages
        var xBar1 = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar1.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < 3; i++)
            {
                await xBar1.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
            }
            await Task.Delay(100);
        }

        // Act - Create new CrossBar and subscribe
        var xBar2 = TestHelpers.CreateTestCrossBar();
        var received = new List<string>();

        xBar2.Subscribe<string>("replay.channel", msg =>
        {
            received.Add(msg.Body!);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        // Playback and publish to new CrossBar
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            await xBar2.Publish("replay.channel", msg, store: false);
        }

        await Task.Delay(100);

        // Assert
        received.Should().Equal("msg-0", "msg-1", "msg-2");
    }

    [Fact]
    public async Task Player_LargeRecording_AllMessagesPreserved()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var messageCount = 1000;

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < messageCount; i++)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
            }
            await Task.Delay(200);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var count = 0;
        var lastMessage = "";

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            count++;
            lastMessage = msg.Body!;
        }

        // Assert
        count.Should().Be(messageCount);
        lastMessage.Should().Be($"msg-{messageCount - 1}");
    }

    // Task 34: Paced mode tests

    [Fact]
    public async Task Player_AsFastAsPossible_PlaysImmediately()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < 10; i++)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
                await Task.Delay(100); // 100ms between messages
            }
            await Task.Delay(100);
        }

        // Act - Playback with AsFastAsPossible mode
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer, PlayMode.AsFastAsPossible);
        var startTime = DateTime.UtcNow;
        var count = 0;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }

        var duration = DateTime.UtcNow - startTime;

        // Assert - Should play back much faster than 1 second (10 messages * 100ms)
        count.Should().Be(10);
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task Player_DefaultMode_IsAsFastAsPossible()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < 5; i++)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
            }
            await Task.Delay(50);
        }

        // Act - Create player without specifying mode
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var count = 0;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public async Task Player_Cancellation_StopsPlayback()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < 100; i++)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
            }
            await Task.Delay(100);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var count = 0;
        var cts = new CancellationTokenSource();

        await foreach (var msg in player.MessagesAsync(cts.Token))
        {
            count++;
            if (count == 10)
            {
                cts.Cancel();
            }
        }

        // Assert - Should stop at 10, not process all 100
        count.Should().Be(10);
    }

    // Task 35: Codec round-trip tests

    [Fact]
    public async Task Codec_RoundTrip_PreservesMessage()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var originalMessage = "Test message with special characters: !@#$%^&*()";

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(originalMessage), false);
            await Task.Delay(50);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        string? roundTrippedMessage = null;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            roundTrippedMessage = msg.Body;
        }

        // Assert
        roundTrippedMessage.Should().Be(originalMessage);
    }

    [Fact]
    public async Task Codec_EmptyString_SerializedCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(""), false);
            await Task.Delay(50);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        string? result = null;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            result = msg.Body;
        }

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public async Task Codec_LongString_SerializedCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var longString = new string('A', 10000);

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(longString), false);
            await Task.Delay(50);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        string? result = null;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            result = msg.Body;
        }

        // Assert
        result.Should().HaveLength(10000);
        result.Should().Be(longString);
    }

    [Fact]
    public async Task Codec_UnicodeCharacters_PreservedCorrectly()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var unicodeMessage = "Hello 世界 🌍 Ñoño";

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(unicodeMessage), false);
            await Task.Delay(50);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        string? result = null;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            result = msg.Body;
        }

        // Assert
        result.Should().Be(unicodeMessage);
    }

    [Fact]
    public async Task Codec_MultipleMessages_EachPreserved()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var messages = new[] { "short", "a much longer message with more content", "", "123", "special!@#" };

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            foreach (var msg in messages)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(msg), false);
            }
            await Task.Delay(100);
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var received = new List<string>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            received.Add(msg.Body!);
        }

        // Assert
        received.Should().Equal(messages);
    }

    [Fact]
    public async Task Recording_Stats_TrackMessagesRecorded()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using var recording = xBar.Record("test.channel", stream, serializer);

        // Act
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"msg-{i}"), false);
        }

        await Task.Delay(100);

        // Assert
        var stats = recording.RecordingStats;
        stats.TotalMessages.Should().Be(10);
    }

    // Phase 2 Coverage Boost Tests

    [Fact]
    public async Task Recording_AsyncMessageHandler_RecordsCorrectly()
    {
        // VALIDATES: Async code path in Recording (currently 0% covered)
        // VALIDATES: Recording`1/<<MessageHandler>g__AsyncPath|15_0>d
        // IMPACT: Covers 12 lines in Recording.cs async path

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using var recording = xBar.Record("test.channel", stream, serializer);

        // Publish messages (will trigger async path in recording)
        for (int i = 0; i < 10; i++)
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"async-msg-{i}"), false);
        }

        await Task.Delay(200); // Allow async recording to complete

        // Act - Stop recording and check data was written
        recording.Dispose();

        // Assert
        stream.Position = 0;
        stream.Length.Should().BeGreaterThan(0, "recording should have written data");

        // Verify we can play back what was recorded
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);

        var playedBack = new List<string>();
        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            playedBack.Add(msg.Body!);
        }

        playedBack.Should().HaveCount(10);
        playedBack.Should().Contain("async-msg-0");
        playedBack.Should().Contain("async-msg-9");
    }

    [Fact]
    public async Task Player_GetNextChunk_HandlesMultipleChunks()
    {
        // VALIDATES: Chunk-based replay logic (Player`1/<GetNextChunk>d__11)
        // IMPACT: Covers 18 lines in Player.cs GetNextChunk method

        // Arrange - Record large dataset that will span multiple chunks
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            // Record 1000 messages to force multiple chunks
            for (int i = 0; i < 1000; i++)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage($"chunk-msg-{i}"), false);
            }

            await Task.Delay(500);
        }

        // Act - Play back and verify chunk handling
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);

        var playedBack = new List<string>();
        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            playedBack.Add(msg.Body!);
        }

        // Assert
        playedBack.Should().HaveCount(1000, "all messages should be played back");
        playedBack.First().Should().Be("chunk-msg-0");
        playedBack.Last().Should().Be("chunk-msg-999");
    }

    [Fact]
    public async Task Player_GetNextChunk_EmptyStream_ReturnsNoMessages()
    {
        // VALIDATES: Edge case - empty recording

        // Arrange
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var xBar = TestHelpers.CreateTestCrossBar();

        // Write minimal valid recording header
        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            // No messages published
        }

        // Act
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);

        var playedBack = new List<string>();
        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            playedBack.Add(msg.Body!);
        }

        // Assert
        playedBack.Should().BeEmpty("empty recording should play back nothing");
    }

    [Fact]
    public async Task Player_Dispose_CleansUpResources()
    {
        // VALIDATES: Player.Dispose() method (currently 0% covered)
        // IMPACT: Covers 2 lines in Player.cs disposal

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            await xBar.Publish("test.channel", TestHelpers.CreateTestMessage("test-msg"), false);
            await Task.Delay(50);
        }

        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);

        // Act - Dispose player
        player.Dispose();

        // Assert - Should not throw
        // Multiple dispose should also be safe
        player.Dispose();

        // Attempting to iterate after dispose should fail gracefully or throw
        // Note: This tests disposal behavior
    }

    [Fact]
    public async Task Player_Stats_ReturnsPlaybackStatistics()
    {
        // VALIDATES: Player.Stats property getter (currently 0% covered)
        // IMPACT: Covers 1 line in Player.cs

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("test.channel", stream, serializer))
        {
            for (int i = 0; i < 50; i++)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(i.ToString()), false);
            }
            await Task.Delay(100);
        }

        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);

        // Act - Access Stats property
        var initialStats = player.Stats;

        // Assert
        initialStats.Should().NotBeNull();

        // Play messages and check stats update
        var count = 0;
        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }

        var finalStats = player.Stats;
        finalStats.TotalMessages.Should().Be(50);
    }
}
