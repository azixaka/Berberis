using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using Berberis.Recorder;
using FluentAssertions;
using System.Buffers;
using Xunit;

namespace Berberis.Messaging.Tests.Recording;

public class RecordingUtilitiesTests
{
    private readonly TestStringSerializer _serializer = new();

    // Helper method to create a test recording file
    private async Task<string> CreateTestRecording(
        string[] messages,
        string? metadataChannel = null,
        long? baseTimestamp = null)
    {
        var tempFile = Path.GetTempFileName();
        baseTimestamp ??= DateTime.UtcNow.Ticks;

        var crossBar = TestHelpers.CreateTestCrossBar();
        var memoryStream = new MemoryStream();

        // Record to MemoryStream (fast, no I/O timing issues)
        using (var recording = crossBar.Record("test.channel", memoryStream, _serializer))
        {
            long timestampOffset = 0;
            long messageId = baseTimestamp.Value; // Use timestamp as unique ID to avoid duplicate ID conflicts when merging
            foreach (var msg in messages)
            {
                var message = new Message<string>(
                    id: messageId++,
                    timestamp: baseTimestamp.Value + timestampOffset,
                    messageType: MessageType.ChannelUpdate,
                    correlationId: 0,
                    key: $"key{timestampOffset}",
                    inceptionTicks: 0,
                    from: "test",
                    body: msg,
                    tagA: 0
                );

                await crossBar.Publish("test.channel", message);
                timestampOffset += TimeSpan.TicksPerSecond; // 1 second between messages
            }

            // Allow pipe to process messages before disposing
            await Task.Delay(100);
        } // Dispose recording

        // Write MemoryStream to file
        await File.WriteAllBytesAsync(tempFile, memoryStream.ToArray());

        // Create metadata if requested
        if (metadataChannel != null)
        {
            var metadata = new RecordingMetadata
            {
                CreatedUtc = DateTime.UtcNow,
                Channel = metadataChannel,
                SerializerType = "TestStringSerializer",
                SerializerVersion = 1,
                MessageType = "System.String",
                MessageCount = messages.Length,
                FirstMessageTicks = baseTimestamp.Value,
                LastMessageTicks = baseTimestamp.Value + (messages.Length - 1) * TimeSpan.TicksPerSecond,
                DurationMs = (messages.Length - 1) * 1000
            };

            var metadataPath = RecordingMetadata.GetMetadataPath(tempFile);
            await RecordingMetadata.WriteAsync(metadata, metadataPath);
        }

        return tempFile;
    }

    #region MergeAsync Tests

    [Fact]
    public async Task MergeAsync_TwoRecordings_MergesByTimestamp()
    {
        // Arrange
        var recording1 = await CreateTestRecording(["msg1", "msg3", "msg5"], "channel1", baseTimestamp: 1000);
        var recording2 = await CreateTestRecording(["msg2", "msg4", "msg6"], "channel2", baseTimestamp: 1000 + TimeSpan.TicksPerSecond / 2);
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            var metadata = await RecordingUtilities.MergeAsync(
                [recording1, recording2],
                outputPath,
                _serializer);

            // Assert
            metadata.Should().NotBeNull();
            metadata.MessageCount.Should().Be(6);

            // Verify merged recording
            await using var stream = File.OpenRead(outputPath);
            var player = Player<string>.Create(stream, _serializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(6);
            // Messages should be ordered by timestamp
            messages[0].Body.Should().Be("msg1");
            messages[1].Body.Should().Be("msg2");
            messages[2].Body.Should().Be("msg3");
            messages[3].Body.Should().Be("msg4");
            messages[4].Body.Should().Be("msg5");
            messages[5].Body.Should().Be("msg6");
        }
        finally
        {
            File.Delete(recording1);
            File.Delete(recording2);
            File.Delete(outputPath);
            TryDeleteMetadata(recording1);
            TryDeleteMetadata(recording2);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task MergeAsync_WithDuplicates_KeepFirst()
    {
        // Arrange
        var baseTimestamp = DateTime.UtcNow.Ticks;
        var recording1 = await CreateTestRecording(["msg1"], "channel1", baseTimestamp: baseTimestamp);
        var recording2 = await CreateTestRecording(["msg2"], "channel2", baseTimestamp: baseTimestamp);
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            var metadata = await RecordingUtilities.MergeAsync(
                [recording1, recording2],
                outputPath,
                _serializer,
                duplicateStrategy: RecordingUtilities.DuplicateStrategy.KeepFirst);

            // Assert - Should keep first message (msg1) since they have same ID (0)
            await using var stream = File.OpenRead(outputPath);
            var player = Player<string>.Create(stream, _serializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(1);
            messages[0].Body.Should().Be("msg1");
        }
        finally
        {
            File.Delete(recording1);
            File.Delete(recording2);
            File.Delete(outputPath);
            TryDeleteMetadata(recording1);
            TryDeleteMetadata(recording2);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task MergeAsync_WithDuplicates_KeepAll()
    {
        // Arrange
        var baseTimestamp = DateTime.UtcNow.Ticks;
        var recording1 = await CreateTestRecording(["msg1"], "channel1", baseTimestamp: baseTimestamp);
        var recording2 = await CreateTestRecording(["msg2"], "channel2", baseTimestamp: baseTimestamp);
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            var metadata = await RecordingUtilities.MergeAsync(
                [recording1, recording2],
                outputPath,
                _serializer,
                duplicateStrategy: RecordingUtilities.DuplicateStrategy.KeepAll);

            // Assert - Should keep both messages
            await using var stream = File.OpenRead(outputPath);
            var player = Player<string>.Create(stream, _serializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(recording1);
            File.Delete(recording2);
            File.Delete(outputPath);
            TryDeleteMetadata(recording1);
            TryDeleteMetadata(recording2);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task MergeAsync_CreatesMetadata()
    {
        // Arrange
        var recording1 = await CreateTestRecording(["msg1"], "channel1");
        var recording2 = await CreateTestRecording(["msg2"], "channel2");
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            await RecordingUtilities.MergeAsync(
                [recording1, recording2],
                outputPath,
                _serializer);

            // Assert
            var metadataPath = RecordingMetadata.GetMetadataPath(outputPath);
            File.Exists(metadataPath).Should().BeTrue();

            var metadata = await RecordingMetadata.ReadAsync(metadataPath);
            metadata.Should().NotBeNull();
            metadata!.MessageCount.Should().Be(2);
            metadata.Custom.Should().ContainKey("mergedFrom");
        }
        finally
        {
            File.Delete(recording1);
            File.Delete(recording2);
            File.Delete(outputPath);
            TryDeleteMetadata(recording1);
            TryDeleteMetadata(recording2);
            TryDeleteMetadata(outputPath);
        }
    }

    #endregion

    #region SplitAsync Tests

    [Fact]
    public async Task SplitAsync_ByMessageCount_CreatesChunks()
    {
        // Arrange
        var recording = await CreateTestRecording(["msg1", "msg2", "msg3", "msg4", "msg5"], "channel");
        var outputPattern = Path.Combine(Path.GetTempPath(), $"split_{Guid.NewGuid()}_{{0}}.rec");

        try
        {
            // Act - Split into chunks of 2 messages each
            var chunks = await RecordingUtilities.SplitAsync(
                recording,
                outputPattern,
                _serializer,
                RecordingUtilities.SplitBy.MessageCount,
                splitValue: 2);

            // Assert
            chunks.Should().HaveCount(3); // 5 messages / 2 = 3 chunks (2, 2, 1)
            chunks[0].MessageCount.Should().Be(2);
            chunks[1].MessageCount.Should().Be(2);
            chunks[2].MessageCount.Should().Be(1);

            // Verify first chunk
            var chunk0Path = string.Format(outputPattern, 0);
            await using var stream = File.OpenRead(chunk0Path);
            var player = Player<string>.Create(stream, _serializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(2);
            messages[0].Body.Should().Be("msg1");
            messages[1].Body.Should().Be("msg2");

            // Cleanup chunks
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunkPath = string.Format(outputPattern, i);
                File.Delete(chunkPath);
                TryDeleteMetadata(chunkPath);
            }
        }
        finally
        {
            File.Delete(recording);
            TryDeleteMetadata(recording);
        }
    }

    [Fact]
    public async Task SplitAsync_ByTimeDuration_CreatesChunks()
    {
        // Arrange
        var recording = await CreateTestRecording(["msg1", "msg2", "msg3", "msg4"], "channel");
        var outputPattern = Path.Combine(Path.GetTempPath(), $"split_{Guid.NewGuid()}_{{0}}.rec");

        try
        {
            // Act - Split by 2 seconds (2 messages per chunk since messages are 1 second apart)
            var chunks = await RecordingUtilities.SplitAsync(
                recording,
                outputPattern,
                _serializer,
                RecordingUtilities.SplitBy.TimeDuration,
                splitValue: TimeSpan.FromSeconds(2).Ticks);

            // Assert
            chunks.Should().HaveCountGreaterThan(0);

            // Cleanup chunks
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunkPath = string.Format(outputPattern, i);
                File.Delete(chunkPath);
                TryDeleteMetadata(chunkPath);
            }
        }
        finally
        {
            File.Delete(recording);
            TryDeleteMetadata(recording);
        }
    }

    [Fact]
    public async Task SplitAsync_CreatesMetadataForEachChunk()
    {
        // Arrange
        var recording = await CreateTestRecording(["msg1", "msg2", "msg3"], "channel");
        var outputPattern = Path.Combine(Path.GetTempPath(), $"split_{Guid.NewGuid()}_{{0}}.rec");

        try
        {
            // Act
            var chunks = await RecordingUtilities.SplitAsync(
                recording,
                outputPattern,
                _serializer,
                RecordingUtilities.SplitBy.MessageCount,
                splitValue: 2);

            // Assert - Each chunk should have metadata
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunkPath = string.Format(outputPattern, i);
                var metadataPath = RecordingMetadata.GetMetadataPath(chunkPath);
                File.Exists(metadataPath).Should().BeTrue($"chunk {i} should have metadata");

                var metadata = await RecordingMetadata.ReadAsync(metadataPath);
                metadata.Should().NotBeNull();
                metadata!.Custom.Should().ContainKey("chunkIndex");
                metadata.Custom!["chunkIndex"].Should().Be(i.ToString());

                File.Delete(chunkPath);
                TryDeleteMetadata(chunkPath);
            }
        }
        finally
        {
            File.Delete(recording);
            TryDeleteMetadata(recording);
        }
    }

    #endregion

    #region FilterAsync Tests

    [Fact]
    public async Task FilterAsync_FiltersByPredicate()
    {
        // Arrange
        var recording = await CreateTestRecording(["msg1", "msg2", "msg3", "msg4", "msg5"], "channel");
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act - Filter messages containing "2" or "4"
            var metadata = await RecordingUtilities.FilterAsync(
                recording,
                outputPath,
                _serializer,
                msg => msg.Body!.Contains("2") || msg.Body!.Contains("4"));

            // Assert
            metadata.MessageCount.Should().Be(2);

            // Verify filtered recording
            await using var stream = File.OpenRead(outputPath);
            var player = Player<string>.Create(stream, _serializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(2);
            messages[0].Body.Should().Be("msg2");
            messages[1].Body.Should().Be("msg4");
        }
        finally
        {
            File.Delete(recording);
            File.Delete(outputPath);
            TryDeleteMetadata(recording);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task FilterAsync_FilterByKey()
    {
        // Arrange
        var recording = await CreateTestRecording(["msg1", "msg2", "msg3"], "channel");
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act - Filter by key
            var metadata = await RecordingUtilities.FilterAsync(
                recording,
                outputPath,
                _serializer,
                msg => msg.Key == "key0");

            // Assert
            metadata.MessageCount.Should().Be(1);

            await using var stream = File.OpenRead(outputPath);
            var player = Player<string>.Create(stream, _serializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(1);
            messages[0].Key.Should().Be("key0");
        }
        finally
        {
            File.Delete(recording);
            File.Delete(outputPath);
            TryDeleteMetadata(recording);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task FilterAsync_FilterByTimeRange()
    {
        // Arrange
        var baseTimestamp = DateTime.UtcNow.Ticks;
        var recording = await CreateTestRecording(["msg1", "msg2", "msg3", "msg4"], "channel", baseTimestamp);
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act - Filter messages in first 2 seconds
            var cutoff = baseTimestamp + 2 * TimeSpan.TicksPerSecond;
            var metadata = await RecordingUtilities.FilterAsync(
                recording,
                outputPath,
                _serializer,
                msg => msg.Timestamp < cutoff);

            // Assert
            metadata.MessageCount.Should().Be(2);
        }
        finally
        {
            File.Delete(recording);
            File.Delete(outputPath);
            TryDeleteMetadata(recording);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task FilterAsync_CreatesMetadata()
    {
        // Arrange
        var recording = await CreateTestRecording(["msg1", "msg2"], "channel");
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            await RecordingUtilities.FilterAsync(
                recording,
                outputPath,
                _serializer,
                msg => true); // Keep all

            // Assert
            var metadataPath = RecordingMetadata.GetMetadataPath(outputPath);
            File.Exists(metadataPath).Should().BeTrue();

            var metadata = await RecordingMetadata.ReadAsync(metadataPath);
            metadata.Should().NotBeNull();
            metadata!.Custom.Should().ContainKey("filteredFrom");
            metadata.Custom.Should().ContainKey("totalInputMessages");
            metadata.Custom.Should().ContainKey("filteredMessages");
        }
        finally
        {
            File.Delete(recording);
            File.Delete(outputPath);
            TryDeleteMetadata(recording);
            TryDeleteMetadata(outputPath);
        }
    }

    #endregion

    #region ConvertAsync Tests

    [Fact]
    public async Task ConvertAsync_ConvertsSerializerVersion()
    {
        // Arrange
        var oldSerializer = new TestStringSerializerV1();
        var newSerializer = new TestStringSerializerV2();

        var recording = await CreateTestRecording(["msg1", "msg2"], "channel");
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            var metadata = await RecordingUtilities.ConvertAsync(
                recording,
                outputPath,
                _serializer, // old
                newSerializer); // new

            // Assert
            metadata.MessageCount.Should().Be(2);
            metadata.SerializerVersion.Should().Be((ushort)((2 << 8) + 0)); // Version 2.0 = 0x0200 = 512

            // Verify converted recording can be read with new serializer
            await using var stream = File.OpenRead(outputPath);
            var player = Player<string>.Create(stream, newSerializer);
            var messages = await ToListAsync(player.MessagesAsync(CancellationToken.None));

            messages.Should().HaveCount(2);
            messages[0].Body.Should().Be("msg1");
            messages[1].Body.Should().Be("msg2");
        }
        finally
        {
            File.Delete(recording);
            File.Delete(outputPath);
            TryDeleteMetadata(recording);
            TryDeleteMetadata(outputPath);
        }
    }

    [Fact]
    public async Task ConvertAsync_CreatesMetadata()
    {
        // Arrange
        var oldSerializer = new TestStringSerializerV1();
        var newSerializer = new TestStringSerializerV2();
        var recording = await CreateTestRecording(["msg1"], "channel");
        var outputPath = Path.GetTempFileName();

        try
        {
            // Act
            await RecordingUtilities.ConvertAsync(
                recording,
                outputPath,
                oldSerializer,
                newSerializer);

            // Assert
            var metadataPath = RecordingMetadata.GetMetadataPath(outputPath);
            File.Exists(metadataPath).Should().BeTrue();

            var metadata = await RecordingMetadata.ReadAsync(metadataPath);
            metadata.Should().NotBeNull();
            metadata!.Custom.Should().ContainKey("convertedFrom");
            metadata.Custom.Should().ContainKey("oldSerializerVersion");
            metadata.Custom.Should().ContainKey("newSerializerVersion");
        }
        finally
        {
            File.Delete(recording);
            File.Delete(outputPath);
            TryDeleteMetadata(recording);
            TryDeleteMetadata(outputPath);
        }
    }

    #endregion

    #region Helper Methods

    private static async Task<List<Message<string>>> ToListAsync(IAsyncEnumerable<Message<string>> source)
    {
        var list = new List<Message<string>>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    private void TryDeleteMetadata(string recordingPath)
    {
        try
        {
            var metadataPath = RecordingMetadata.GetMetadataPath(recordingPath);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    // Test serializers with different versions
    private class TestStringSerializerV1 : IMessageBodySerializer<string>
    {
        public SerializerVersion Version => new SerializerVersion(1, 0);
        public void Serialize(string value, IBufferWriter<byte> writer) => new TestStringSerializer().Serialize(value, writer);
        public string Deserialize(ReadOnlySpan<byte> data) => new TestStringSerializer().Deserialize(data);
    }

    private class TestStringSerializerV2 : IMessageBodySerializer<string>
    {
        public SerializerVersion Version => new SerializerVersion(2, 0);
        public void Serialize(string value, IBufferWriter<byte> writer) => new TestStringSerializer().Serialize(value, writer);
        public string Deserialize(ReadOnlySpan<byte> data) => new TestStringSerializer().Deserialize(data);
    }

    #endregion
}
