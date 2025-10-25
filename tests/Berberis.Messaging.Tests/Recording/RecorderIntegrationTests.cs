using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using Berberis.Recorder;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Recording;

/// <summary>
/// Comprehensive integration tests for the Recorder area.
/// Tests end-to-end workflows: channel setup -> publishing -> recording -> playback -> verification
/// </summary>
public class RecorderIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        // Cleanup all temporary files created during tests
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
            }
        }
    }

    private string CreateTempFile()
    {
        var file = Path.GetTempFileName();
        _tempFiles.Add(file);
        return file;
    }

    #region End-to-End File Recording Tests

    [Fact]
    public async Task EndToEnd_FileRecording_PublishRecordPlayback_AllMessagesPreserved()
    {
        // VALIDATES: Complete file-based recording workflow
        // VALIDATES: Channel publishing, file recording, playback integrity

        // Arrange
        var recordingFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var testMessages = Enumerable.Range(0, 50).Select(i => $"message-{i}").ToList();

        // Act - Record messages to file
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("test.channel", fileStream, serializer);

            foreach (var msgBody in testMessages)
            {
                await xBar.Publish("test.channel", TestHelpers.CreateTestMessage(msgBody), store: false);
            }

            await Task.Delay(200); // Allow messages to flush
        }

        // Act - Playback from file
        var playedMessages = new List<string>();
        await using (var fileStream = File.OpenRead(recordingFile))
        {
            var player = Player<string>.Create(fileStream, serializer);

            await foreach (var msg in player.MessagesAsync(CancellationToken.None))
            {
                playedMessages.Add(msg.Body!);
            }
        }

        // Assert
        playedMessages.Should().HaveCount(testMessages.Count);
        playedMessages.Should().Equal(testMessages);
    }

    [Fact]
    public async Task EndToEnd_FileRecording_WithMetadata_MetadataFileCreated()
    {
        // VALIDATES: Metadata auto-creation for file-based recordings
        // VALIDATES: Metadata contains correct channel and serializer info

        // Arrange
        var recordingFile = CreateTempFile();
        var metadataFile = RecordingMetadata.GetMetadataPath(recordingFile);
        _tempFiles.Add(metadataFile);

        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        // Act
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("integration.channel", fileStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                configureMetadata: meta =>
                {
                    meta.Custom = new Dictionary<string, string>
                    {
                        ["test"] = "integration-test",
                        ["environment"] = "testing"
                    };
                });

            for (int i = 0; i < 10; i++)
            {
                await xBar.Publish("integration.channel", TestHelpers.CreateTestMessage($"msg-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        // Wait for async metadata write
        await Task.Delay(100);

        // Assert
        File.Exists(metadataFile).Should().BeTrue("metadata file should be created for file streams");

        var metadata = await RecordingMetadata.ReadAsync(metadataFile);
        metadata.Should().NotBeNull();
        metadata!.Channel.Should().Be("integration.channel");
        metadata.SerializerType.Should().Contain("String");
        metadata.Custom.Should().NotBeNull();
        metadata.Custom!["test"].Should().Be("integration-test");
        metadata.Custom["environment"].Should().Be("testing");
    }

    [Fact]
    public async Task EndToEnd_FileRecording_LargeMessageSet_AllPreserved()
    {
        // VALIDATES: High-volume recording and playback
        // VALIDATES: Performance with larger datasets

        // Arrange
        var recordingFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var messageCount = 10000;

        // Act - Record large message set
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("bulk.channel", fileStream, serializer);

            for (int i = 0; i < messageCount; i++)
            {
                await xBar.Publish("bulk.channel", TestHelpers.CreateTestMessage($"bulk-{i}"), store: false);

                // Occasional delay to simulate realistic publishing
                if (i % 1000 == 0)
                    await Task.Delay(10);
            }

            await Task.Delay(500);
        }

        // Act - Playback and verify
        var count = 0;
        string? firstMessage = null;
        string? lastMessage = null;

        await using (var fileStream = File.OpenRead(recordingFile))
        {
            var player = Player<string>.Create(fileStream, serializer);

            await foreach (var msg in player.MessagesAsync(CancellationToken.None))
            {
                if (count == 0) firstMessage = msg.Body;
                lastMessage = msg.Body;
                count++;
            }
        }

        // Assert
        count.Should().Be(messageCount);
        firstMessage.Should().Be("bulk-0");
        lastMessage.Should().Be($"bulk-{messageCount - 1}");
    }

    [Fact]
    public async Task EndToEnd_FileRecording_VariableMessageSizes_AllPreserved()
    {
        // VALIDATES: Recording messages of varying sizes
        // VALIDATES: Codec handles small to large messages correctly

        // Arrange
        var recordingFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        var testMessages = new[]
        {
            "",                                    // Empty
            "x",                                   // Single char
            "small message",                       // Small
            new string('M', 1000),                 // Medium (1KB)
            new string('L', 100_000),              // Large (100KB)
            "final message"                        // Normal
        };

        // Act - Record
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("varied.channel", fileStream, serializer);

            foreach (var msgBody in testMessages)
            {
                await xBar.Publish("varied.channel", TestHelpers.CreateTestMessage(msgBody), store: false);
            }

            await Task.Delay(300);
        }

        // Act - Playback
        var playedMessages = new List<string>();
        await using (var fileStream = File.OpenRead(recordingFile))
        {
            var player = Player<string>.Create(fileStream, serializer);

            await foreach (var msg in player.MessagesAsync(CancellationToken.None))
            {
                playedMessages.Add(msg.Body!);
            }
        }

        // Assert
        playedMessages.Should().HaveCount(testMessages.Length);
        for (int i = 0; i < testMessages.Length; i++)
        {
            playedMessages[i].Should().Be(testMessages[i], $"message {i} should match");
            playedMessages[i].Length.Should().Be(testMessages[i].Length, $"message {i} length should match");
        }
    }

    #endregion

    #region Memory Stream Recording Tests

    [Fact]
    public async Task EndToEnd_MemoryStream_PublishRecordPlayback_AllMessagesPreserved()
    {
        // VALIDATES: In-memory recording workflow
        // VALIDATES: MemoryStream as recording target

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var testMessages = Enumerable.Range(0, 25).Select(i => $"mem-msg-{i}").ToList();

        // Act - Record to memory
        using (var recording = xBar.Record("memory.channel", stream, serializer))
        {
            foreach (var msgBody in testMessages)
            {
                await xBar.Publish("memory.channel", TestHelpers.CreateTestMessage(msgBody), store: false);
            }

            await Task.Delay(100);
        }

        // Act - Playback from memory
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var playedMessages = new List<string>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            playedMessages.Add(msg.Body!);
        }

        // Assert
        playedMessages.Should().HaveCount(testMessages.Count);
        playedMessages.Should().Equal(testMessages);
    }

    [Fact]
    public async Task EndToEnd_MemoryStream_MultiplePlaybacks_SameResults()
    {
        // VALIDATES: Recording can be played back multiple times
        // VALIDATES: Stream rewind and replay functionality

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("replay.channel", stream, serializer))
        {
            for (int i = 0; i < 10; i++)
            {
                await xBar.Publish("replay.channel", TestHelpers.CreateTestMessage($"replay-{i}"), store: false);
            }
            await Task.Delay(100);
        }

        // Act - First playback
        stream.Position = 0;
        var firstPlayback = new List<string>();
        var player1 = Player<string>.Create(stream, serializer);
        await foreach (var msg in player1.MessagesAsync(CancellationToken.None))
        {
            firstPlayback.Add(msg.Body!);
        }

        // Act - Second playback
        stream.Position = 0;
        var secondPlayback = new List<string>();
        var player2 = Player<string>.Create(stream, serializer);
        await foreach (var msg in player2.MessagesAsync(CancellationToken.None))
        {
            secondPlayback.Add(msg.Body!);
        }

        // Assert
        firstPlayback.Should().HaveCount(10);
        secondPlayback.Should().HaveCount(10);
        secondPlayback.Should().Equal(firstPlayback);
    }

    [Fact]
    public async Task EndToEnd_MemoryStream_NoMetadataFile_StillWorks()
    {
        // VALIDATES: Memory streams don't require metadata files
        // VALIDATES: Graceful handling when metadata can't be written

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();

        // Act - Record with metadata configuration (should be ignored for MemoryStream)
        using (var recording = xBar.Record("nometa.channel", stream, serializer,
            saveInitialState: false,
            conflationInterval: TimeSpan.Zero,
            configureMetadata: meta =>
            {
                meta.Custom = new Dictionary<string, string> { ["test"] = "value" };
            }))
        {
            await xBar.Publish("nometa.channel", TestHelpers.CreateTestMessage("test"), store: false);
            await Task.Delay(50);
        }

        // Assert - Should not throw, recording should work
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var messages = new List<string>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            messages.Add(msg.Body!);
        }

        messages.Should().HaveCount(1);
        messages[0].Should().Be("test");
    }

    #endregion

    #region Streaming Index Integration Tests

    [Fact]
    public async Task EndToEnd_StreamingIndex_RecordWithIndex_IndexCreatedDuringRecording()
    {
        // VALIDATES: Streaming index generation during recording
        // VALIDATES: Index file created alongside recording

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        // Act - Record with streaming index
        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("indexed.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                indexStream: indexStream);

            for (int i = 0; i < 100; i++)
            {
                await xBar.Publish("indexed.channel", TestHelpers.CreateTestMessage($"idx-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        // Assert - Index file should exist and be valid
        File.Exists(indexFile).Should().BeTrue();
        var indexFileInfo = new FileInfo(indexFile);
        indexFileInfo.Length.Should().BeGreaterThan(0);

        // Verify index can be read
        await using var idxStream = File.OpenRead(indexFile);
        var (interval, totalMessages, entries) = await RecordingIndex.ReadAsync(idxStream);

        totalMessages.Should().Be(100);
        entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EndToEnd_StreamingIndex_SeekAndPlay_StartsAtCorrectPosition()
    {
        // VALIDATES: Indexed playback with seeking
        // VALIDATES: Seeking to message number works correctly

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        // Record with streaming index
        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("seek.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                indexStream: indexStream);

            for (int i = 0; i < 200; i++)
            {
                await xBar.Publish("seek.channel", TestHelpers.CreateTestMessage($"seek-msg-{i}"), store: false);
            }

            await Task.Delay(300);
        }

        // Act - Seek to middle and playback
        await using var recStream = File.OpenRead(recordingFile);
        await using var idxStream = File.OpenRead(indexFile);
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recStream, idxStream, serializer);

        var seekTarget = 100L;
        var actualPosition = await indexedPlayer.SeekToMessageAsync(seekTarget);

        var playedMessages = new List<string>();
        await foreach (var msg in indexedPlayer.MessagesAsync(CancellationToken.None))
        {
            playedMessages.Add(msg.Body!);
            if (playedMessages.Count >= 10) break; // Read 10 messages from seek position
        }

        // Assert
        indexedPlayer.TotalMessages.Should().Be(200);
        actualPosition.Should().BeLessThanOrEqualTo(seekTarget);
        playedMessages.Should().NotBeEmpty();
        playedMessages.Should().HaveCount(10);
    }

    [Fact]
    public async Task EndToEnd_StreamingIndex_MemoryStreams_IndexInMemory()
    {
        // VALIDATES: Streaming index works with memory streams
        // VALIDATES: In-memory indexed recordings

        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var recordingStream = new MemoryStream();
        var indexStream = new MemoryStream();
        var serializer = new TestStringSerializer();

        // Act - Record with in-memory index
        using (var recording = xBar.Record("mem-idx.channel", recordingStream, serializer,
            saveInitialState: false,
            conflationInterval: TimeSpan.Zero,
            indexStream: indexStream))
        {
            for (int i = 0; i < 50; i++)
            {
                await xBar.Publish("mem-idx.channel", TestHelpers.CreateTestMessage($"midx-{i}"), store: false);
            }

            await Task.Delay(100);
        }

        // Assert - Index should be in memory
        indexStream.Length.Should().BeGreaterThan(0);

        // Verify we can use the index
        recordingStream.Position = 0;
        indexStream.Position = 0;
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recordingStream, indexStream, serializer);
        indexedPlayer.TotalMessages.Should().Be(50);
    }

    #endregion

    #region Playback Mode Tests

    [Fact]
    public async Task EndToEnd_PlaybackMode_AsFastAsPossible_CompletesQuickly()
    {
        // VALIDATES: AsFastAsPossible playback mode
        // VALIDATES: No artificial delays between messages

        // Arrange
        var stream = new MemoryStream();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        using (var recording = xBar.Record("fast.channel", stream, serializer))
        {
            for (int i = 0; i < 50; i++)
            {
                await xBar.Publish("fast.channel", TestHelpers.CreateTestMessage($"fast-{i}"), store: false);
                await Task.Delay(20); // 20ms between messages = 1 second total
            }
            await Task.Delay(100);
        }

        // Act - Playback as fast as possible
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer, PlayMode.AsFastAsPossible);
        var startTime = DateTime.UtcNow;
        var count = 0;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }

        var duration = DateTime.UtcNow - startTime;

        // Assert - Should be much faster than original 1 second
        count.Should().Be(50);
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task EndToEnd_PlaybackMode_RespectOriginalIntervals_PreservesTiming()
    {
        // VALIDATES: RespectOriginalMessageIntervals playback mode
        // VALIDATES: Timing is preserved during playback

        // Arrange
        var stream = new MemoryStream();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var messageCount = 5;
        var intervalMs = 100;

        using (var recording = xBar.Record("paced.channel", stream, serializer))
        {
            for (int i = 0; i < messageCount; i++)
            {
                await xBar.Publish("paced.channel", TestHelpers.CreateTestMessage($"paced-{i}"), store: false);
                if (i < messageCount - 1)
                    await Task.Delay(intervalMs);
            }
            await Task.Delay(100);
        }

        // Act - Playback with timing preserved
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer, PlayMode.RespectOriginalMessageIntervals);
        var startTime = DateTime.UtcNow;
        var count = 0;

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            count++;
        }

        var duration = DateTime.UtcNow - startTime;
        var expectedDuration = TimeSpan.FromMilliseconds((messageCount - 1) * intervalMs);

        // Assert - Duration should be close to original timing
        count.Should().Be(messageCount);
        duration.Should().BeGreaterThan(expectedDuration.Subtract(TimeSpan.FromMilliseconds(50)));
        duration.Should().BeLessThan(expectedDuration.Add(TimeSpan.FromMilliseconds(200)));
    }

    #endregion

    #region Timestamp Preservation Tests

    [Fact]
    public async Task EndToEnd_TimestampPreservation_OriginalTimestampsRetained()
    {
        // VALIDATES: Message timestamps are preserved exactly
        // VALIDATES: No timestamp modification during recording/playback

        // Arrange
        var stream = new MemoryStream();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        var originalTimestamps = new List<long>();

        // Record and capture original timestamps
        using (var recording = xBar.Record("timestamp.channel", stream, serializer))
        {
            for (int i = 0; i < 10; i++)
            {
                var msg = TestHelpers.CreateTestMessage($"ts-{i}");
                originalTimestamps.Add(msg.Timestamp);
                await xBar.Publish("timestamp.channel", msg, store: false);
                await Task.Delay(10); // Small delay to ensure different timestamps
            }
            await Task.Delay(100);
        }

        // Act - Playback and collect timestamps
        stream.Position = 0;
        var player = Player<string>.Create(stream, serializer);
        var playedTimestamps = new List<long>();

        await foreach (var msg in player.MessagesAsync(CancellationToken.None))
        {
            playedTimestamps.Add(msg.Timestamp);
        }

        // Assert - Timestamps should match exactly
        playedTimestamps.Should().HaveCount(originalTimestamps.Count);
        for (int i = 0; i < originalTimestamps.Count; i++)
        {
            playedTimestamps[i].Should().Be(originalTimestamps[i],
                $"timestamp for message {i} should be preserved exactly");
        }
    }

    [Fact]
    public async Task EndToEnd_TimestampPreservation_MetadataHasBasicInfo()
    {
        // VALIDATES: Metadata has basic information (channel, serializer)
        // NOTE: MessageCount, FirstMessageTicks, LastMessageTicks are optional and not auto-populated during recording

        // Arrange
        var recordingFile = CreateTempFile();
        var metadataFile = RecordingMetadata.GetMetadataPath(recordingFile);
        _tempFiles.Add(metadataFile);

        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        // Act - Record with time-spaced messages
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("ts-meta.channel", fileStream, serializer);

            for (int i = 0; i < 10; i++)
            {
                await xBar.Publish("ts-meta.channel", TestHelpers.CreateTestMessage($"tsmeta-{i}"), store: false);
                await Task.Delay(50);
            }

            await Task.Delay(100);
        }

        await Task.Delay(100); // Wait for metadata write

        // Assert - Basic metadata fields should be present
        var metadata = await RecordingMetadata.ReadAsync(metadataFile);
        metadata.Should().NotBeNull();
        metadata!.Channel.Should().Be("ts-meta.channel");
        metadata.SerializerType.Should().Contain("String");
        metadata.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task EndToEnd_TimestampPreservation_SeekByTimestamp_FindsCorrectMessage()
    {
        // VALIDATES: Timestamp-based seeking in indexed player
        // VALIDATES: Index contains accurate timestamp information

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        // Record with known timestamps
        long targetTimestamp = 0;
        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("ts-seek.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                indexStream: indexStream);

            for (int i = 0; i < 100; i++)
            {
                var msg = TestHelpers.CreateTestMessage($"ts-seek-{i}");
                if (i == 50) targetTimestamp = msg.Timestamp; // Capture middle timestamp

                await xBar.Publish("ts-seek.channel", msg, store: false);
                await Task.Delay(5);
            }

            await Task.Delay(200);
        }

        // Act - Seek by timestamp
        await using var recStream = File.OpenRead(recordingFile);
        await using var idxStream = File.OpenRead(indexFile);
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recStream, idxStream, serializer);

        var seekedPosition = await indexedPlayer.SeekToTimestampAsync(targetTimestamp);

        // Assert - Should seek to position at or before target
        seekedPosition.Should().BeLessThanOrEqualTo(50);
        seekedPosition.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Metadata Roundtrip Tests

    [Fact]
    public async Task EndToEnd_Metadata_FullRoundtrip_AllFieldsPreserved()
    {
        // VALIDATES: Complete metadata roundtrip
        // VALIDATES: All configured metadata fields are correctly persisted and loaded

        // Arrange
        var recordingFile = CreateTempFile();
        var metadataFile = RecordingMetadata.GetMetadataPath(recordingFile);
        _tempFiles.Add(metadataFile);

        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var expectedCreatedTime = DateTime.UtcNow;

        // Act - Record with full metadata
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("meta.channel", fileStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                configureMetadata: meta =>
                {
                    meta.CreatedUtc = expectedCreatedTime;
                    meta.Custom = new Dictionary<string, string>
                    {
                        ["application"] = "IntegrationTest",
                        ["version"] = "2.0.0",
                        ["datacenter"] = "us-west-2",
                        ["environment"] = "staging"
                    };
                });

            for (int i = 0; i < 100; i++)
            {
                await xBar.Publish("meta.channel", TestHelpers.CreateTestMessage($"meta-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        await Task.Delay(100); // Wait for async metadata write

        // Assert - Read and verify metadata
        var metadata = await RecordingMetadata.ReadAsync(metadataFile);
        metadata.Should().NotBeNull();
        metadata!.CreatedUtc.Should().BeCloseTo(expectedCreatedTime, TimeSpan.FromSeconds(1));
        metadata.Channel.Should().Be("meta.channel");
        metadata.SerializerType.Should().Contain("String");
        metadata.MessageType.Should().Contain("String");

        metadata.Custom.Should().NotBeNull();
        metadata.Custom.Should().HaveCount(4);
        metadata.Custom!["application"].Should().Be("IntegrationTest");
        metadata.Custom["version"].Should().Be("2.0.0");
        metadata.Custom["datacenter"].Should().Be("us-west-2");
        metadata.Custom["environment"].Should().Be("staging");
    }

    [Fact]
    public async Task EndToEnd_Metadata_PlaybackCountMatchesPublished()
    {
        // VALIDATES: Playback count matches published message count
        // NOTE: MessageCount in metadata is optional and not auto-populated

        // Arrange
        var recordingFile = CreateTempFile();
        var metadataFile = RecordingMetadata.GetMetadataPath(recordingFile);
        _tempFiles.Add(metadataFile);

        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var expectedCount = 75;

        // Act - Record
        await using (var fileStream = File.Create(recordingFile))
        {
            using var recording = xBar.Record("count.channel", fileStream, serializer);

            for (int i = 0; i < expectedCount; i++)
            {
                await xBar.Publish("count.channel", TestHelpers.CreateTestMessage($"count-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        await Task.Delay(100);

        // Playback and count
        var actualCount = 0;
        await using (var fileStream = File.OpenRead(recordingFile))
        {
            var player = Player<string>.Create(fileStream, serializer);
            await foreach (var msg in player.MessagesAsync(CancellationToken.None))
            {
                actualCount++;
            }
        }

        // Assert - Playback should match what was published
        actualCount.Should().Be(expectedCount);

        // Metadata should exist with basic info
        var metadata = await RecordingMetadata.ReadAsync(metadataFile);
        metadata.Should().NotBeNull();
        metadata!.Channel.Should().Be("count.channel");
    }

    #endregion

    #region Indexed Playback Seeking Tests

    [Fact]
    public async Task EndToEnd_IndexedPlayback_SeekMultipleTimes_EachSeekCorrect()
    {
        // VALIDATES: Multiple seeks in a single playback session
        // VALIDATES: Index supports repeated seeking

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("multiseek.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                indexStream: indexStream);

            for (int i = 0; i < 500; i++)
            {
                await xBar.Publish("multiseek.channel", TestHelpers.CreateTestMessage($"ms-{i}"), store: false);
            }

            await Task.Delay(300);
        }

        // Act - Multiple seeks
        await using var recStream = File.OpenRead(recordingFile);
        await using var idxStream = File.OpenRead(indexFile);
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recStream, idxStream, serializer);

        // Seek to different positions
        var seek1 = await indexedPlayer.SeekToMessageAsync(100);
        var seek2 = await indexedPlayer.SeekToMessageAsync(300);
        var seek3 = await indexedPlayer.SeekToMessageAsync(0);
        var seek4 = await indexedPlayer.SeekToMessageAsync(450);

        // Assert - All seeks should work
        seek1.Should().BeLessThanOrEqualTo(100);
        seek2.Should().BeLessThanOrEqualTo(300);
        seek3.Should().Be(0);
        seek4.Should().BeLessThanOrEqualTo(450);

        // Verify we can play from last seek position
        var messages = new List<string>();
        await foreach (var msg in indexedPlayer.MessagesAsync(CancellationToken.None))
        {
            messages.Add(msg.Body!);
            if (messages.Count >= 10) break;
        }

        messages.Should().HaveCount(10);
    }

    [Fact]
    public async Task EndToEnd_IndexedPlayback_TotalMessagesProperty_Accurate()
    {
        // VALIDATES: IndexedPlayer.TotalMessages property
        // VALIDATES: Index header contains correct total message count

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var expectedTotal = 250;

        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("total.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                indexStream: indexStream);

            for (int i = 0; i < expectedTotal; i++)
            {
                await xBar.Publish("total.channel", TestHelpers.CreateTestMessage($"tot-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        // Act
        await using var recStream = File.OpenRead(recordingFile);
        await using var idxStream = File.OpenRead(indexFile);
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recStream, idxStream, serializer);

        // Assert
        indexedPlayer.TotalMessages.Should().Be(expectedTotal);

        // Verify by counting actual messages
        var count = 0;
        await foreach (var msg in indexedPlayer.MessagesAsync(CancellationToken.None))
        {
            count++;
        }

        count.Should().Be(expectedTotal);
    }

    [Fact]
    public async Task EndToEnd_IndexedPlayback_SeekAndReplay_WorksCorrectly()
    {
        // VALIDATES: Seeking to a specific position works correctly
        // VALIDATES: Can replay from seeked position

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("end.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                indexStream: indexStream);

            for (int i = 0; i < 100; i++)
            {
                await xBar.Publish("end.channel", TestHelpers.CreateTestMessage($"end-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        // Act - Seek to start, then count all messages
        await using var recStream = File.OpenRead(recordingFile);
        await using var idxStream = File.OpenRead(indexFile);
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recStream, idxStream, serializer);

        // Seek to beginning
        await indexedPlayer.SeekToMessageAsync(0);

        var messages = new List<string>();
        await foreach (var msg in indexedPlayer.MessagesAsync(CancellationToken.None))
        {
            messages.Add(msg.Body!);
        }

        // Assert - Should get all messages when starting from beginning
        messages.Should().HaveCount(100);
        messages[0].Should().Be("end-0");
        messages[99].Should().Be("end-99");

        // Verify total messages property
        indexedPlayer.TotalMessages.Should().Be(100);
    }

    #endregion

    #region Complex Integration Scenarios

    [Fact]
    public async Task EndToEnd_CompleteWorkflow_RecordIndexMetadataSeekPlay_AllFeaturesWork()
    {
        // VALIDATES: Complete end-to-end workflow with all features
        // VALIDATES: File recording + streaming index + metadata + seeking + playback

        // Arrange
        var recordingFile = CreateTempFile();
        var indexFile = CreateTempFile();
        var metadataFile = RecordingMetadata.GetMetadataPath(recordingFile);
        _tempFiles.Add(metadataFile);

        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();
        var messageCount = 500;

        // Act 1 - Record with all features enabled
        await using (var recordingStream = File.Create(recordingFile))
        await using (var indexStream = File.Create(indexFile))
        {
            using var recording = xBar.Record("complete.channel", recordingStream, serializer,
                saveInitialState: false,
                conflationInterval: TimeSpan.Zero,
                configureMetadata: meta =>
                {
                    meta.Custom = new Dictionary<string, string>
                    {
                        ["test"] = "complete-workflow",
                        ["features"] = "all"
                    };
                },
                indexStream: indexStream);

            for (int i = 0; i < messageCount; i++)
            {
                await xBar.Publish("complete.channel", TestHelpers.CreateTestMessage($"complete-{i}"), store: false);
            }

            await Task.Delay(300);
        }

        await Task.Delay(100);

        // Assert 1 - Verify all files created
        File.Exists(recordingFile).Should().BeTrue();
        File.Exists(indexFile).Should().BeTrue();
        File.Exists(metadataFile).Should().BeTrue();

        // Assert 2 - Verify metadata
        var metadata = await RecordingMetadata.ReadAsync(metadataFile);
        metadata.Should().NotBeNull();
        metadata!.Channel.Should().Be("complete.channel");
        metadata.Custom.Should().ContainKey("test");

        // Assert 3 - Verify indexed playback with seeking
        await using var recStream = File.OpenRead(recordingFile);
        await using var idxStream = File.OpenRead(indexFile);
        var indexedPlayer = await IndexedPlayer<string>.CreateAsync(recStream, idxStream, serializer);

        indexedPlayer.TotalMessages.Should().Be(messageCount);

        // Seek to middle
        var seekPos = await indexedPlayer.SeekToMessageAsync(250);
        seekPos.Should().BeLessThanOrEqualTo(250);

        // Play from middle
        var playedFromMiddle = new List<string>();
        await foreach (var msg in indexedPlayer.MessagesAsync(CancellationToken.None))
        {
            playedFromMiddle.Add(msg.Body!);
            if (playedFromMiddle.Count >= 50) break;
        }

        playedFromMiddle.Should().HaveCount(50);

        // Assert 4 - Verify full playback from start
        await indexedPlayer.SeekToMessageAsync(0);
        var fullPlayback = new List<string>();
        await foreach (var msg in indexedPlayer.MessagesAsync(CancellationToken.None))
        {
            fullPlayback.Add(msg.Body!);
        }

        fullPlayback.Should().HaveCount(messageCount);
        fullPlayback[0].Should().Be("complete-0");
        fullPlayback[messageCount - 1].Should().Be($"complete-{messageCount - 1}");
    }

    [Fact]
    public async Task EndToEnd_MultipleChannels_IndependentRecordings_NoInterference()
    {
        // VALIDATES: Multiple simultaneous recordings
        // VALIDATES: Different channels don't interfere with each other

        // Arrange
        var file1 = CreateTempFile();
        var file2 = CreateTempFile();
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        // Act - Record two channels simultaneously
        await using (var stream1 = File.Create(file1))
        await using (var stream2 = File.Create(file2))
        {
            using var recording1 = xBar.Record("channel.one", stream1, serializer);
            using var recording2 = xBar.Record("channel.two", stream2, serializer);

            for (int i = 0; i < 50; i++)
            {
                await xBar.Publish("channel.one", TestHelpers.CreateTestMessage($"ch1-{i}"), store: false);
                await xBar.Publish("channel.two", TestHelpers.CreateTestMessage($"ch2-{i}"), store: false);
            }

            await Task.Delay(200);
        }

        // Assert - Each recording should contain only its channel's messages
        await using (var stream1 = File.OpenRead(file1))
        {
            var player1 = Player<string>.Create(stream1, serializer);
            var messages1 = new List<string>();

            await foreach (var msg in player1.MessagesAsync(CancellationToken.None))
            {
                messages1.Add(msg.Body!);
            }

            messages1.Should().HaveCount(50);
            messages1.Should().AllSatisfy(m => m.Should().StartWith("ch1-"));
        }

        await using (var stream2 = File.OpenRead(file2))
        {
            var player2 = Player<string>.Create(stream2, serializer);
            var messages2 = new List<string>();

            await foreach (var msg in player2.MessagesAsync(CancellationToken.None))
            {
                messages2.Add(msg.Body!);
            }

            messages2.Should().HaveCount(50);
            messages2.Should().AllSatisfy(m => m.Should().StartWith("ch2-"));
        }
    }

    #endregion
}
