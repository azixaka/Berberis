using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using Berberis.Recorder;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Recording;

public class RecordingMetadataTests
{
    [Fact]
    public async Task Metadata_WriteAndRead_Roundtrips()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var metadata = new RecordingMetadata
            {
                CreatedUtc = new DateTime(2025, 10, 25, 12, 30, 0, DateTimeKind.Utc),
                Channel = "test.channel",
                SerializerType = "TestStringSerializer",
                SerializerVersion = 1,
                MessageType = "System.String",
                MessageCount = 100,
                FirstMessageTicks = 123456789,
                LastMessageTicks = 987654321,
                DurationMs = 5000,
                IndexFile = "recording.rec.idx",
                Custom = new Dictionary<string, string>
                {
                    ["application"] = "TestApp",
                    ["version"] = "1.0.0"
                }
            };

            // Act
            await RecordingMetadata.WriteAsync(metadata, tempFile);
            var readMetadata = await RecordingMetadata.ReadAsync(tempFile);

            // Assert
            readMetadata.Should().NotBeNull();
            readMetadata!.CreatedUtc.Should().Be(metadata.CreatedUtc);
            readMetadata.Channel.Should().Be(metadata.Channel);
            readMetadata.SerializerType.Should().Be(metadata.SerializerType);
            readMetadata.SerializerVersion.Should().Be(metadata.SerializerVersion);
            readMetadata.MessageType.Should().Be(metadata.MessageType);
            readMetadata.MessageCount.Should().Be(metadata.MessageCount);
            readMetadata.FirstMessageTicks.Should().Be(metadata.FirstMessageTicks);
            readMetadata.LastMessageTicks.Should().Be(metadata.LastMessageTicks);
            readMetadata.DurationMs.Should().Be(metadata.DurationMs);
            readMetadata.IndexFile.Should().Be(metadata.IndexFile);
            readMetadata.Custom.Should().NotBeNull();
            readMetadata.Custom.Should().ContainKey("application");
            readMetadata.Custom!["application"].Should().Be("TestApp");
            readMetadata.Custom.Should().ContainKey("version");
            readMetadata.Custom["version"].Should().Be("1.0.0");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Metadata_ReadNonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".meta.json");

        // Act
        var metadata = await RecordingMetadata.ReadAsync(nonExistentFile);

        // Assert
        metadata.Should().BeNull();
    }

    [Fact]
    public async Task Metadata_MinimalMetadata_WritesAndReads()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var metadata = new RecordingMetadata
            {
                CreatedUtc = DateTime.UtcNow,
                Channel = "minimal.channel"
            };

            // Act
            await RecordingMetadata.WriteAsync(metadata, tempFile);
            var readMetadata = await RecordingMetadata.ReadAsync(tempFile);

            // Assert
            readMetadata.Should().NotBeNull();
            readMetadata!.CreatedUtc.Should().BeCloseTo(metadata.CreatedUtc, TimeSpan.FromSeconds(1));
            readMetadata.Channel.Should().Be(metadata.Channel);
            readMetadata.MessageCount.Should().BeNull();
            readMetadata.Custom.Should().BeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetMetadataPath_AppendsCorrectExtension()
    {
        // Arrange
        var recordingPath = "/path/to/recording.rec";

        // Act
        var metadataPath = RecordingMetadata.GetMetadataPath(recordingPath);

        // Assert
        metadataPath.Should().Be("/path/to/recording.rec.meta.json");
    }

    [Fact]
    public async Task Record_WithMetadata_WritesMetadataFile()
    {
        // Arrange
        var tempRecordingFile = Path.GetTempFileName();
        var metadataPath = RecordingMetadata.GetMetadataPath(tempRecordingFile);

        try
        {
            var xBar = TestHelpers.CreateTestCrossBar();
            var serializer = new TestStringSerializer();
            var metadata = new RecordingMetadata
            {
                CreatedUtc = DateTime.UtcNow,
                Channel = "test.channel",
                SerializerType = "TestStringSerializer",
                SerializerVersion = 1,
                MessageType = "System.String"
            };

            // Act
            using (var fileStream = File.Create(tempRecordingFile))
            {
                using var recording = xBar.Record("test.channel", fileStream, serializer, saveInitialState: false,
                    conflationInterval: TimeSpan.Zero, metadata: metadata);

                // Publish some test messages
                for (int i = 0; i < 5; i++)
                {
                    var msg = TestHelpers.CreateTestMessage($"msg-{i}");
                    await xBar.Publish("test.channel", msg, store: false);
                }

                await Task.Delay(200);
            }

            // Wait a bit for async write to complete
            await Task.Delay(100);

            // Assert
            File.Exists(metadataPath).Should().BeTrue();
            var readMetadata = await RecordingMetadata.ReadAsync(metadataPath);
            readMetadata.Should().NotBeNull();
            readMetadata!.Channel.Should().Be("test.channel");
            readMetadata.SerializerType.Should().Be("TestStringSerializer");
        }
        finally
        {
            if (File.Exists(tempRecordingFile))
                File.Delete(tempRecordingFile);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
    }

    [Fact]
    public async Task Record_WithoutMetadata_DoesNotWriteMetadataFile()
    {
        // Arrange
        var tempRecordingFile = Path.GetTempFileName();
        var metadataPath = RecordingMetadata.GetMetadataPath(tempRecordingFile);

        try
        {
            var xBar = TestHelpers.CreateTestCrossBar();
            var serializer = new TestStringSerializer();

            // Act
            using (var fileStream = File.Create(tempRecordingFile))
            {
                using var recording = xBar.Record("test.channel", fileStream, serializer);

                // Publish some test messages
                for (int i = 0; i < 5; i++)
                {
                    var msg = TestHelpers.CreateTestMessage($"msg-{i}");
                    await xBar.Publish("test.channel", msg, store: false);
                }

                await Task.Delay(200);
            }

            // Wait a bit to ensure no async write happens
            await Task.Delay(100);

            // Assert - backwards compatibility: no metadata file should be created
            File.Exists(metadataPath).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempRecordingFile))
                File.Delete(tempRecordingFile);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
    }

    [Fact]
    public async Task Metadata_JsonFormat_IsReadable()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var metadata = new RecordingMetadata
            {
                CreatedUtc = new DateTime(2025, 10, 25, 12, 30, 0, DateTimeKind.Utc),
                Channel = "test.channel",
                MessageCount = 42
            };

            // Act
            await RecordingMetadata.WriteAsync(metadata, tempFile);
            var jsonContent = await File.ReadAllTextAsync(tempFile);

            // Assert - JSON should be human-readable (indented)
            jsonContent.Should().Contain("\"channel\": \"test.channel\"");
            jsonContent.Should().Contain("\"messageCount\": 42");
            jsonContent.Should().Contain(Environment.NewLine); // Indented JSON has newlines
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Record_WithMemoryStream_DoesNotWriteMetadata()
    {
        // Arrange
        var xBar = TestHelpers.CreateTestCrossBar();
        var stream = new MemoryStream();
        var serializer = new TestStringSerializer();
        var metadata = new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = "test.channel"
        };

        // Act - using MemoryStream, not FileStream
        using (var recording = xBar.Record("test.channel", stream, serializer, saveInitialState: false,
            conflationInterval: TimeSpan.Zero, metadata: metadata))
        {
            for (int i = 0; i < 5; i++)
            {
                var msg = TestHelpers.CreateTestMessage($"msg-{i}");
                await xBar.Publish("test.channel", msg, store: false);
            }

            await Task.Delay(200);
        }

        // Assert - no exception should be thrown, metadata simply ignored for non-FileStreams
        stream.Length.Should().BeGreaterThan(0);
    }
}
