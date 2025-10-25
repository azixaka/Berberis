using System.Buffers.Binary;

namespace Berberis.Recorder;

/// <summary>
/// Represents an entry in a recording index.
/// </summary>
/// <param name="MessageNumber">The sequential message number (0-based).</param>
/// <param name="FileOffset">The byte offset in the recording file where this message starts.</param>
/// <param name="Timestamp">The message timestamp (ticks).</param>
public readonly record struct IndexEntry(long MessageNumber, long FileOffset, long Timestamp);

/// <summary>
/// Builds and reads recording indexes for fast seeking.
/// </summary>
/// <remarks>
/// Index files use a binary format:
/// <code>
/// Offset  Size    Field
/// ------  ------  --------------------------------------------------
/// 0       4       Magic ("RIDX" = 0x58444952)
/// 4       2       Version (uint16 = 1)
/// 6       2       Reserved
/// 8       4       Index Interval (messages between index entries)
/// 12      8       Total Messages (int64)
/// 20      8       Entry Count (int64)
/// 28      N*24    Index Entries (MessageNumber:int64, FileOffset:int64, Timestamp:int64)
/// </code>
/// </remarks>
public static class RecordingIndex
{
    private const uint MagicNumber = 0x58444952; // "RIDX" in little-endian
    private const ushort FormatVersion = 1;
    private const int HeaderSize = 28;
    private const int EntrySize = 24; // 3 * sizeof(long)

    /// <summary>
    /// Default indexing interval (index every Nth message).
    /// </summary>
    public const int DefaultInterval = 1000;

    /// <summary>
    /// Builds an index from an existing recording stream.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="recordingStream">The recording stream (must be seekable).</param>
    /// <param name="indexStream">The stream to write the index to (must be writable and seekable).</param>
    /// <param name="serializer">The message body serializer.</param>
    /// <param name="interval">Index every Nth message (default: 1000).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of index entries created.</returns>
    /// <exception cref="ArgumentException">Thrown if recordingStream is not seekable or indexStream is not writable/seekable.</exception>
    public static async Task<long> BuildAsync<TBody>(
        Stream recordingStream,
        Stream indexStream,
        IMessageBodySerializer<TBody> serializer,
        int interval = DefaultInterval,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!recordingStream.CanSeek)
            throw new ArgumentException("Recording stream must be seekable for index building", nameof(recordingStream));

        if (!indexStream.CanWrite)
            throw new ArgumentException("Index stream must be writable", nameof(indexStream));

        if (!indexStream.CanSeek)
            throw new ArgumentException("Index stream must be seekable", nameof(indexStream));

        if (interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive");

        var recordingSize = recordingStream.Length;
        var entries = new List<IndexEntry>();
        long messageNumber = 0;
        long fileOffset = 0;

        // Read recording and build index entries
        var player = Player<TBody>.Create(recordingStream, serializer);

        await foreach (var message in player.MessagesAsync(cancellationToken))
        {
            if (messageNumber % interval == 0)
            {
                entries.Add(new IndexEntry(messageNumber, fileOffset, message.Timestamp));
            }

            // Calculate next file offset (current position in stream)
            fileOffset = recordingStream.Position;
            messageNumber++;

            // Report progress
            if (progress != null && messageNumber % 1000 == 0)
            {
                var progressData = new RecordingProgress(
                    BytesProcessed: fileOffset,
                    TotalBytes: recordingSize,
                    MessagesProcessed: messageNumber,
                    PercentComplete: recordingSize > 0 ? (double)fileOffset / recordingSize * 100.0 : 0
                );
                progress.Report(progressData);
            }
        }

        // Write index file
        await WriteIndexAsync(indexStream, interval, messageNumber, entries, cancellationToken);

        return entries.Count;
    }

    /// <summary>
    /// Reads an index from a stream.
    /// </summary>
    /// <param name="indexStream">The index stream to read from (must be readable).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Index metadata and entries.</returns>
    /// <exception cref="ArgumentException">Thrown if indexStream is not readable.</exception>
    /// <exception cref="InvalidDataException">Thrown if the index format is invalid or unsupported.</exception>
    public static async Task<(int Interval, long TotalMessages, IndexEntry[] Entries)> ReadAsync(
        Stream indexStream,
        CancellationToken cancellationToken = default)
    {
        if (!indexStream.CanRead)
            throw new ArgumentException("Index stream must be readable", nameof(indexStream));

        // Read header
        var headerBuffer = new byte[HeaderSize];
        await indexStream.ReadExactlyAsync(headerBuffer, cancellationToken);

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.AsSpan(0));
        if (magic != MagicNumber)
            throw new InvalidDataException($"Invalid index file magic: 0x{magic:X8}");

        var version = BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer.AsSpan(4));
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported index version: {version}");

        var interval = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(8));
        var totalMessages = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer.AsSpan(12));
        var entryCount = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer.AsSpan(20));

        // Read entries
        var entries = new IndexEntry[entryCount];
        var entryBuffer = new byte[EntrySize];

        for (long i = 0; i < entryCount; i++)
        {
            await indexStream.ReadExactlyAsync(entryBuffer, cancellationToken);

            var messageNumber = BinaryPrimitives.ReadInt64LittleEndian(entryBuffer.AsSpan(0));
            var fileOffset = BinaryPrimitives.ReadInt64LittleEndian(entryBuffer.AsSpan(8));
            var timestamp = BinaryPrimitives.ReadInt64LittleEndian(entryBuffer.AsSpan(16));

            entries[i] = new IndexEntry(messageNumber, fileOffset, timestamp);
        }

        return (interval, totalMessages, entries);
    }

    private static async Task WriteIndexAsync(
        Stream stream,
        int interval,
        long totalMessages,
        List<IndexEntry> entries,
        CancellationToken cancellationToken)
    {
        var headerBuffer = new byte[HeaderSize];

        // Write header
        BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(0), MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(4), FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(6), 0); // Reserved
        BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(8), interval);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(12), totalMessages);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(20), entries.Count);

        await stream.WriteAsync(headerBuffer, cancellationToken);

        // Write entries
        var entryBuffer = new byte[EntrySize];

        foreach (var entry in entries)
        {
            BinaryPrimitives.WriteInt64LittleEndian(entryBuffer.AsSpan(0), entry.MessageNumber);
            BinaryPrimitives.WriteInt64LittleEndian(entryBuffer.AsSpan(8), entry.FileOffset);
            BinaryPrimitives.WriteInt64LittleEndian(entryBuffer.AsSpan(16), entry.Timestamp);

            await stream.WriteAsync(entryBuffer, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the conventional index file path for a recording file.
    /// </summary>
    /// <param name="recordingPath">Path to the recording file (e.g., "recording.rec").</param>
    /// <returns>Path to the index file (e.g., "recording.rec.idx").</returns>
    public static string GetIndexPath(string recordingPath)
    {
        return $"{recordingPath}.idx";
    }

    /// <summary>
    /// Performs a binary search to find the index entry for a given message number.
    /// </summary>
    /// <param name="entries">Sorted index entries.</param>
    /// <param name="messageNumber">The message number to find.</param>
    /// <returns>The index of the entry at or before the target message number.</returns>
    public static int FindEntryForMessage(IndexEntry[] entries, long messageNumber)
    {
        if (entries.Length == 0)
            return -1;

        if (messageNumber < entries[0].MessageNumber)
            return -1;

        if (messageNumber >= entries[^1].MessageNumber)
            return entries.Length - 1;

        int left = 0;
        int right = entries.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (entries[mid].MessageNumber == messageNumber)
                return mid;

            if (entries[mid].MessageNumber < messageNumber)
                left = mid + 1;
            else
                right = mid - 1;
        }

        // Return the entry before the target (floor)
        return right;
    }

    /// <summary>
    /// Performs a binary search to find the index entry for a given timestamp.
    /// </summary>
    /// <param name="entries">Sorted index entries.</param>
    /// <param name="timestamp">The timestamp (ticks) to find.</param>
    /// <returns>The index of the entry at or before the target timestamp.</returns>
    public static int FindEntryForTimestamp(IndexEntry[] entries, long timestamp)
    {
        if (entries.Length == 0)
            return -1;

        if (timestamp < entries[0].Timestamp)
            return -1;

        if (timestamp >= entries[^1].Timestamp)
            return entries.Length - 1;

        int left = 0;
        int right = entries.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (entries[mid].Timestamp == timestamp)
                return mid;

            if (entries[mid].Timestamp < timestamp)
                left = mid + 1;
            else
                right = mid - 1;
        }

        // Return the entry before the target (floor)
        return right;
    }
}

/// <summary>
/// Progress information for recording operations.
/// </summary>
/// <param name="BytesProcessed">Number of bytes processed so far.</param>
/// <param name="TotalBytes">Total number of bytes to process.</param>
/// <param name="MessagesProcessed">Number of messages processed so far.</param>
/// <param name="PercentComplete">Percentage complete (0-100).</param>
public readonly record struct RecordingProgress(
    long BytesProcessed,
    long TotalBytes,
    long MessagesProcessed,
    double PercentComplete);
