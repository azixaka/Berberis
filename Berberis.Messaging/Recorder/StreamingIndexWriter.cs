using System.Buffers.Binary;

namespace Berberis.Recorder;

/// <summary>
/// Writes recording index entries incrementally during recording.
/// </summary>
/// <remarks>
/// This class enables index building during recording rather than as a post-processing step.
/// The index file is written incrementally with a placeholder header, then finalized with
/// the correct total message count when recording completes.
/// </remarks>
internal sealed class StreamingIndexWriter : IDisposable
{
    private const uint MagicNumber = 0x58444952; // "RIDX" in little-endian
    private const ushort FormatVersion = 1;
    private const int HeaderSize = 28;
    private const int EntrySize = 24; // 3 * sizeof(long)

    private readonly Stream _indexStream;
    private readonly int _interval;
    private long _entryCount;
    private bool _isDisposed;

    /// <summary>
    /// Creates a new streaming index writer.
    /// </summary>
    /// <param name="indexPath">Path to the index file to create.</param>
    /// <param name="interval">Index every Nth message (default: 1000).</param>
    public StreamingIndexWriter(string indexPath, int interval = RecordingIndex.DefaultInterval)
    {
        if (interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive");

        _interval = interval;
        _indexStream = File.Create(indexPath);
        _entryCount = 0;

        // Write placeholder header (we don't know total messages yet)
        WritePlaceholderHeader();
    }

    /// <summary>
    /// Writes a message index entry if this message number should be indexed.
    /// </summary>
    /// <param name="messageNumber">The sequential message number (0-based).</param>
    /// <param name="fileOffset">The byte offset in the recording file where this message starts.</param>
    /// <param name="timestamp">The message timestamp (ticks).</param>
    /// <returns>True if an entry was written, false if skipped due to interval.</returns>
    public bool TryWriteEntry(long messageNumber, long fileOffset, long timestamp)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StreamingIndexWriter));

        if (messageNumber % _interval != 0)
            return false;

        var entry = new IndexEntry(messageNumber, fileOffset, timestamp);
        WriteEntry(entry);
        _entryCount++;
        return true;
    }

    /// <summary>
    /// Finalizes the index file by writing the correct header with total message count.
    /// </summary>
    /// <param name="totalMessages">The total number of messages in the recording.</param>
    public void Finalize(long totalMessages)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StreamingIndexWriter));

        // Flush any buffered entries
        _indexStream.Flush();

        // Rewrite header with correct total messages and entry count
        _indexStream.Position = 0;
        WriteHeader(totalMessages);
        _indexStream.Flush();
    }

    /// <summary>
    /// Finalizes the index file asynchronously.
    /// </summary>
    /// <param name="totalMessages">The total number of messages in the recording.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task FinalizeAsync(long totalMessages, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StreamingIndexWriter));

        // Flush any buffered entries
        await _indexStream.FlushAsync(cancellationToken);

        // Rewrite header with correct total messages and entry count
        _indexStream.Position = 0;
        await WriteHeaderAsync(totalMessages, cancellationToken);
        await _indexStream.FlushAsync(cancellationToken);
    }

    private void WritePlaceholderHeader()
    {
        var headerBuffer = new byte[HeaderSize];

        BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(0), MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(4), FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(6), 0); // Reserved
        BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(8), _interval);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(12), 0); // Placeholder total messages
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(20), 0); // Placeholder entry count

        _indexStream.Write(headerBuffer);
    }

    private void WriteHeader(long totalMessages)
    {
        var headerBuffer = new byte[HeaderSize];

        BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(0), MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(4), FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(6), 0); // Reserved
        BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(8), _interval);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(12), totalMessages);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(20), _entryCount);

        _indexStream.Write(headerBuffer);
    }

    private async Task WriteHeaderAsync(long totalMessages, CancellationToken cancellationToken)
    {
        var headerBuffer = new byte[HeaderSize];

        BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(0), MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(4), FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBuffer.AsSpan(6), 0); // Reserved
        BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(8), _interval);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(12), totalMessages);
        BinaryPrimitives.WriteInt64LittleEndian(headerBuffer.AsSpan(20), _entryCount);

        await _indexStream.WriteAsync(headerBuffer, cancellationToken);
    }

    private void WriteEntry(IndexEntry entry)
    {
        var entryBuffer = new byte[EntrySize];

        BinaryPrimitives.WriteInt64LittleEndian(entryBuffer.AsSpan(0), entry.MessageNumber);
        BinaryPrimitives.WriteInt64LittleEndian(entryBuffer.AsSpan(8), entry.FileOffset);
        BinaryPrimitives.WriteInt64LittleEndian(entryBuffer.AsSpan(16), entry.Timestamp);

        _indexStream.Write(entryBuffer);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _indexStream?.Dispose();
        _isDisposed = true;
    }
}
