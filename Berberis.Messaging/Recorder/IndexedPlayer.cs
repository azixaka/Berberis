using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

/// <summary>
/// Indexed player implementation with seeking capabilities.
/// </summary>
/// <typeparam name="TBody">The message body type.</typeparam>
public sealed class IndexedPlayer<TBody> : IIndexedPlayer<TBody>
{
    private readonly Stream _stream;
    private readonly IMessageBodySerializer<TBody> _serializer;
    private readonly PlayMode _playMode;
    private readonly int _indexInterval;
    private readonly IndexEntry[] _indexEntries;
    private IPlayer<TBody> _currentPlayer;

    private IndexedPlayer(
        Stream stream,
        IMessageBodySerializer<TBody> serializer,
        PlayMode playMode,
        int indexInterval,
        long totalMessages,
        IndexEntry[] indexEntries)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _playMode = playMode;
        _indexInterval = indexInterval;
        TotalMessages = totalMessages;
        _indexEntries = indexEntries ?? throw new ArgumentNullException(nameof(indexEntries));

        // Create initial player at start of stream
        _currentPlayer = Player<TBody>.Create(stream, serializer, playMode);
    }

    /// <inheritdoc/>
    public long TotalMessages { get; }

    /// <inheritdoc/>
    public RecorderStats Stats => _currentPlayer.Stats;

    /// <summary>
    /// Creates an indexed player from a recording and its index stream.
    /// </summary>
    /// <param name="recordingStream">The recording stream (must be seekable).</param>
    /// <param name="indexStream">The index stream (must be readable).</param>
    /// <param name="serializer">The message body serializer.</param>
    /// <param name="playMode">The playback mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An indexed player instance.</returns>
    /// <exception cref="ArgumentException">Thrown if recordingStream is not seekable or indexStream is not readable.</exception>
    public static async Task<IIndexedPlayer<TBody>> CreateAsync(
        Stream recordingStream,
        Stream indexStream,
        IMessageBodySerializer<TBody> serializer,
        PlayMode playMode = PlayMode.AsFastAsPossible,
        CancellationToken cancellationToken = default)
    {
        if (!recordingStream.CanSeek)
            throw new ArgumentException("Recording stream must be seekable for indexed playback", nameof(recordingStream));

        if (!indexStream.CanRead)
            throw new ArgumentException("Index stream must be readable", nameof(indexStream));

        // Load index
        var (interval, totalMessages, entries) = await RecordingIndex.ReadAsync(indexStream, cancellationToken);

        return new IndexedPlayer<TBody>(recordingStream, serializer, playMode, interval, totalMessages, entries);
    }

    /// <inheritdoc/>
    public Task<long> SeekToMessageAsync(long messageNumber, CancellationToken cancellationToken = default)
    {
        if (messageNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(messageNumber), "Message number must be non-negative");

        if (messageNumber >= TotalMessages)
            throw new ArgumentOutOfRangeException(nameof(messageNumber), $"Message number {messageNumber} exceeds total messages {TotalMessages}");

        // Find the index entry for this message
        var entryIndex = RecordingIndex.FindEntryForMessage(_indexEntries, messageNumber);

        if (entryIndex < 0)
        {
            // Before first indexed message, seek to start of stream
            _stream.Position = 0;
            RecreatePlayer();
            return Task.FromResult(0L);
        }

        // Seek to the file offset for this index entry
        var entry = _indexEntries[entryIndex];
        _stream.Position = entry.FileOffset;
        RecreatePlayer();

        return Task.FromResult(entry.MessageNumber);
    }

    /// <inheritdoc/>
    public Task<long> SeekToTimestampAsync(long timestamp, CancellationToken cancellationToken = default)
    {
        // Find the index entry for this timestamp
        var entryIndex = RecordingIndex.FindEntryForTimestamp(_indexEntries, timestamp);

        if (entryIndex < 0)
        {
            // Before first indexed message, seek to start of stream
            _stream.Position = 0;
            RecreatePlayer();
            return Task.FromResult(0L);
        }

        // Seek to the file offset for this index entry
        var entry = _indexEntries[entryIndex];
        _stream.Position = entry.FileOffset;
        RecreatePlayer();

        return Task.FromResult(entry.MessageNumber);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<Message<TBody>> MessagesAsync(CancellationToken token)
    {
        return _currentPlayer.MessagesAsync(token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _currentPlayer?.Dispose();
    }

    private void RecreatePlayer()
    {
        // Dispose old player and create new one at current stream position
        _currentPlayer?.Dispose();
        _currentPlayer = Player<TBody>.Create(_stream, _serializer, _playMode);
    }
}
