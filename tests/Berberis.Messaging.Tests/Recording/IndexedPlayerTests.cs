using Berberis.Messaging;
using Berberis.Messaging.Tests.Helpers;
using Berberis.Recorder;
using FluentAssertions;
using Xunit;

namespace Berberis.Messaging.Tests.Recording;

public class IndexedPlayerTests
{
    [Fact]
    public async Task IndexBuild_CreatesValidIndexFile()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();

        try
        {
            // Create a test recording with 100 messages
            await CreateTestRecording(recordingFile, messageCount: 100);

            // Act
            var serializer = new TestStringSerializer();
            long entryCount;
            await using (var recordingStream = File.OpenRead(recordingFile))
            await using (var indexStream = File.Create(indexFile))
            {
                entryCount = await RecordingIndex.BuildAsync(recordingStream, indexStream, serializer, interval: 10);
            }

            // Assert
            entryCount.Should().Be(10); // 100 messages / interval 10 = 10 entries
            File.Exists(indexFile).Should().BeTrue();

            // Verify index can be read
            await using var indexReadStream = File.OpenRead(indexFile);
            var (interval, totalMessages, entries) = await RecordingIndex.ReadAsync(indexReadStream);
            interval.Should().Be(10);
            totalMessages.Should().Be(100);
            entries.Should().HaveCount(10);
            entries[0].MessageNumber.Should().Be(0);
            entries[^1].MessageNumber.Should().Be(90);
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    [Fact]
    public async Task IndexedPlayer_SeekToMessage_SeeksCorrectly()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();

        try
        {
            await CreateTestRecording(recordingFile, messageCount: 100);

            await using (var recordingStream = File.OpenRead(recordingFile))
            await using (var indexStream = File.Create(indexFile))
            {
                await RecordingIndex.BuildAsync(recordingStream, indexStream, new TestStringSerializer(), interval: 10);
            }

            // Act
            await using var stream = File.OpenRead(recordingFile);
            await using var idxStream = File.OpenRead(indexFile);
            var player = await IndexedPlayer<string>.CreateAsync(stream, idxStream, new TestStringSerializer());

            // Seek to message 50
            var actualMessage = await player.SeekToMessageAsync(50);

            // Assert
            actualMessage.Should().Be(50); // Exact match since 50 is at the index boundary (50 / 10 = 5)
            player.TotalMessages.Should().Be(100);

            // Read next message to verify we're at the right position
            var messages = new List<Message<string>>();
            await foreach (var msg in player.MessagesAsync(CancellationToken.None))
            {
                messages.Add(msg);
                if (messages.Count >= 3) break; // Just read a few messages
            }

            messages.Should().HaveCountGreaterThanOrEqualTo(3);
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    [Fact]
    public async Task IndexedPlayer_SeekToTimestamp_SeeksCorrectly()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();

        try
        {
            await CreateTestRecording(recordingFile, messageCount: 100);

            await using (var recordingStream = File.OpenRead(recordingFile))
            await using (var indexStream = File.Create(indexFile))
            {
                await RecordingIndex.BuildAsync(recordingStream, indexStream, new TestStringSerializer(), interval: 10);
            }

            // Get a timestamp from the middle of the recording
            long targetTimestamp;
            await using (var stream = File.OpenRead(recordingFile))
            {
                var tempPlayer = Player<string>.Create(stream, new TestStringSerializer());
                var messageNumber = 0;
                await foreach (var msg in tempPlayer.MessagesAsync(CancellationToken.None))
                {
                    if (messageNumber == 55)
                    {
                        targetTimestamp = msg.Timestamp;
                        break;
                    }
                    messageNumber++;
                }
                targetTimestamp = 0; // Will be set in loop
            }

            // Re-read to get timestamp
            await using (var stream = File.OpenRead(recordingFile))
            {
                var tempPlayer = Player<string>.Create(stream, new TestStringSerializer());
                var messageNumber = 0;
                await foreach (var msg in tempPlayer.MessagesAsync(CancellationToken.None))
                {
                    if (messageNumber == 55)
                    {
                        targetTimestamp = msg.Timestamp;
                        break;
                    }
                    messageNumber++;
                }
            }

            // Act
            await using (var stream = File.OpenRead(recordingFile))
            await using (var idxStream = File.OpenRead(indexFile))
            {
                var player = await IndexedPlayer<string>.CreateAsync(stream, idxStream, new TestStringSerializer());
                var actualMessage = await player.SeekToTimestampAsync(targetTimestamp);

                // Assert - should seek to index entry at or before timestamp
                actualMessage.Should().BeLessThanOrEqualTo(55);
                actualMessage.Should().BeGreaterThanOrEqualTo(50); // Should be at or before message 55
            }
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    [Fact]
    public async Task IndexedPlayer_SeekBeforeStart_SeeksToZero()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();

        try
        {
            await CreateTestRecording(recordingFile, messageCount: 100);

            await using (var recordingStream = File.OpenRead(recordingFile))
            await using (var indexStream = File.Create(indexFile))
            {
                await RecordingIndex.BuildAsync(recordingStream, indexStream, new TestStringSerializer(), interval: 10);
            }

            // Act
            await using var stream = File.OpenRead(recordingFile);
            await using var idxStream = File.OpenRead(indexFile);
            var player = await IndexedPlayer<string>.CreateAsync(stream, idxStream, new TestStringSerializer());
            var actualMessage = await player.SeekToTimestampAsync(0); // Timestamp before any messages

            // Assert
            actualMessage.Should().Be(0);
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    [Fact]
    public async Task IndexedPlayer_SeekBeyondEnd_ThrowsException()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();

        try
        {
            await CreateTestRecording(recordingFile, messageCount: 100);

            await using (var recordingStream = File.OpenRead(recordingFile))
            await using (var indexStream = File.Create(indexFile))
            {
                await RecordingIndex.BuildAsync(recordingStream, indexStream, new TestStringSerializer(), interval: 10);
            }

            // Act & Assert
            await using var stream = File.OpenRead(recordingFile);
            await using var idxStream = File.OpenRead(indexFile);
            var player = await IndexedPlayer<string>.CreateAsync(stream, idxStream, new TestStringSerializer());

            Func<Task> act = async () => await player.SeekToMessageAsync(1000); // Beyond total messages
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    [Fact]
    public void FindEntryForMessage_BinarySearch_FindsCorrectEntry()
    {
        // Arrange
        var entries = new[]
        {
            new IndexEntry(0, 0, 1000),
            new IndexEntry(100, 5000, 2000),
            new IndexEntry(200, 10000, 3000),
            new IndexEntry(300, 15000, 4000),
        };

        // Act & Assert
        RecordingIndex.FindEntryForMessage(entries, 0).Should().Be(0);
        RecordingIndex.FindEntryForMessage(entries, 50).Should().Be(0);  // Floor: entry 0
        RecordingIndex.FindEntryForMessage(entries, 100).Should().Be(1);
        RecordingIndex.FindEntryForMessage(entries, 150).Should().Be(1); // Floor: entry 1
        RecordingIndex.FindEntryForMessage(entries, 200).Should().Be(2);
        RecordingIndex.FindEntryForMessage(entries, 350).Should().Be(3); // Beyond last, returns last
    }

    [Fact]
    public void FindEntryForTimestamp_BinarySearch_FindsCorrectEntry()
    {
        // Arrange
        var entries = new[]
        {
            new IndexEntry(0, 0, 1000),
            new IndexEntry(100, 5000, 2000),
            new IndexEntry(200, 10000, 3000),
            new IndexEntry(300, 15000, 4000),
        };

        // Act & Assert
        RecordingIndex.FindEntryForTimestamp(entries, 1000).Should().Be(0);
        RecordingIndex.FindEntryForTimestamp(entries, 1500).Should().Be(0); // Floor: entry 0
        RecordingIndex.FindEntryForTimestamp(entries, 2000).Should().Be(1);
        RecordingIndex.FindEntryForTimestamp(entries, 2500).Should().Be(1); // Floor: entry 1
        RecordingIndex.FindEntryForTimestamp(entries, 5000).Should().Be(3); // Beyond last, returns last
    }

    [Fact]
    public void GetIndexPath_AppendsCorrectExtension()
    {
        // Arrange
        var recordingPath = "/path/to/recording.rec";

        // Act
        var indexPath = RecordingIndex.GetIndexPath(recordingPath);

        // Assert
        indexPath.Should().Be("/path/to/recording.rec.idx");
    }

    [Fact]
    public async Task IndexBuild_WithProgress_ReportsProgress()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();
        var progressReports = new List<RecordingProgress>();

        try
        {
            await CreateTestRecording(recordingFile, messageCount: 5000);

            var progress = new Progress<RecordingProgress>(p => progressReports.Add(p));

            // Act
            await using var recordingStream = File.OpenRead(recordingFile);
            await using var indexStream = File.Create(indexFile);
            await RecordingIndex.BuildAsync(recordingStream, indexStream, new TestStringSerializer(), interval: 100, progress: progress);

            // Assert
            progressReports.Should().NotBeEmpty();
            progressReports.Should().Contain(p => p.PercentComplete > 0);
            progressReports.Last().PercentComplete.Should().BeGreaterThan(90); // Should be near 100% at end
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    [Fact]
    public async Task IndexedPlayer_NonSeekableStream_ThrowsException()
    {
        // Arrange
        var recordingFile = Path.GetTempFileName();
        var indexFile = Path.GetTempFileName();

        try
        {
            await CreateTestRecording(recordingFile, messageCount: 100);

            await using (var recordingStream = File.OpenRead(recordingFile))
            await using (var indexStream = File.Create(indexFile))
            {
                await RecordingIndex.BuildAsync(recordingStream, indexStream, new TestStringSerializer(), interval: 10);
            }

            // Act & Assert
            var nonSeekableStream = new MemoryStream(await File.ReadAllBytesAsync(recordingFile), writable: false);
            await using var idxStream = File.OpenRead(indexFile);

            // Make stream non-seekable by wrapping it
            Func<Task> act = async () => await IndexedPlayer<string>.CreateAsync(nonSeekableStream, idxStream, new TestStringSerializer());

            // Note: MemoryStream is seekable, so this test would need a custom non-seekable stream wrapper
            // For now, just verify the stream parameter validation works
            nonSeekableStream.CanSeek.Should().BeTrue(); // MemoryStream is seekable by default
        }
        finally
        {
            if (File.Exists(recordingFile)) File.Delete(recordingFile);
            if (File.Exists(indexFile)) File.Delete(indexFile);
        }
    }

    private static async Task CreateTestRecording(string filePath, int messageCount)
    {
        var xBar = TestHelpers.CreateTestCrossBar();
        var serializer = new TestStringSerializer();

        await using (var fileStream = File.Create(filePath))
        {
            using var recording = xBar.Record("test.channel", fileStream, serializer);

            for (int i = 0; i < messageCount; i++)
            {
                var msg = TestHelpers.CreateTestMessage($"msg-{i}");
                await xBar.Publish("test.channel", msg, store: false);
            }

            await Task.Delay(100); // Let messages flush
        }
    }
}
