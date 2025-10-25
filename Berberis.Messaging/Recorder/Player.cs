using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Berberis.Recorder;

/// <summary>
/// Plays back recorded messages from a stream.
/// </summary>
/// <remarks>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Minimal allocations: Uses ArrayPool for header buffers (28 bytes), property caching for Key/From</description></item>
/// <item><description>Throughput: Capable of ~5-10M messages/second depending on deserialization cost</description></item>
/// <item><description>Streaming: Messages are read and deserialized on-demand via IAsyncEnumerable</description></item>
/// <item><description>Timing control: Supports both fast-as-possible and original-interval playback modes</description></item>
/// </list>
/// </remarks>
/// <typeparam name="TBody">The message body type.</typeparam>
public sealed partial class Player<TBody> : IPlayer<TBody>
{
    private Stream _stream;
    private IMessageBodySerializer<TBody> _serialiser;
    private PlayMode _playMode;
    private readonly RecorderStatsReporter _recorderStatsReporter = new();
    private long? _previousTimestamp;
    private readonly IProgress<RecordingProgress>? _progress;
    private readonly long _streamLength;
    private long _messagesProcessed;

    private Player(Stream stream, IMessageBodySerializer<TBody> serialiser, PlayMode playMode, IProgress<RecordingProgress>? progress = null)
    {
        _stream = stream;
        _serialiser = serialiser;
        _playMode = playMode;
        _progress = progress;
        _streamLength = stream.CanSeek ? stream.Length : 0;
        _messagesProcessed = 0;
    }

    /// <summary>Gets playback statistics.</summary>
    public RecorderStats Stats => _recorderStatsReporter.GetStats();

    /// <summary>
    /// Creates a player for recorded messages.
    /// </summary>
    /// <param name="stream">The stream containing recorded messages.</param>
    /// <param name="serialiser">The message body serializer.</param>
    /// <returns>A player instance.</returns>
    public static IPlayer<TBody> Create(Stream stream, IMessageBodySerializer<TBody> serialiser) =>
           Create(stream, serialiser, PlayMode.AsFastAsPossible);

    /// <summary>
    /// Creates a player for recorded messages with specified play mode.
    /// </summary>
    /// <param name="stream">The stream containing recorded messages.</param>
    /// <param name="serialiser">The message body serializer.</param>
    /// <param name="playMode">The playback mode.</param>
    /// <returns>A player instance.</returns>
    public static IPlayer<TBody> Create(Stream stream, IMessageBodySerializer<TBody> serialiser, PlayMode playMode) =>
           new Player<TBody>(stream, serialiser, playMode, progress: null);

    /// <summary>
    /// Creates a player for recorded messages with progress reporting.
    /// </summary>
    /// <param name="stream">The stream containing recorded messages.</param>
    /// <param name="serialiser">The message body serializer.</param>
    /// <param name="playMode">The playback mode.</param>
    /// <param name="progress">Progress reporter (optional).</param>
    /// <returns>A player instance.</returns>
    public static IPlayer<TBody> Create(Stream stream, IMessageBodySerializer<TBody> serialiser, PlayMode playMode, IProgress<RecordingProgress>? progress) =>
           new Player<TBody>(stream, serialiser, playMode, progress);

    /// <summary>
    /// Gets messages from the recording asynchronously.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>An async enumerable of recorded messages.</returns>
    public async IAsyncEnumerable<Message<TBody>> MessagesAsync([EnumeratorCancellation] CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var ticks = _recorderStatsReporter.Start();

            var chunkResult = await GetNextChunk(token);

            if (chunkResult.HasValue)
            {
                var chunk = chunkResult.Value;

                try
                {
                    var obj = _serialiser.Deserialize(chunk.Body);

                    var message = new Message<TBody>(chunk.Id, chunk.TimestampTicks, chunk.Type, 0, chunk.Key, 0, chunk.From, obj, 0);

                    _recorderStatsReporter.Stop(ticks, chunk.Length);

                    // Respect original message timing if requested
                    if (_playMode == PlayMode.RespectOriginalMessageIntervals && _previousTimestamp.HasValue)
                    {
                        var delay = chunk.TimestampTicks - _previousTimestamp.Value;
                        if (delay > 0)
                        {
                            var delayTimeSpan = TimeSpan.FromTicks(delay);
                            await Task.Delay(delayTimeSpan, token);
                        }
                    }

                    _previousTimestamp = chunk.TimestampTicks;
                    _messagesProcessed++;

                    // Report progress every 1000 messages
                    if (_progress != null && _messagesProcessed % 1000 == 0)
                    {
                        var bytesProcessed = _stream.CanSeek ? _stream.Position : 0;
                        var percentComplete = _streamLength > 0 ? (double)bytesProcessed / _streamLength * 100.0 : 0;

                        _progress.Report(new RecordingProgress(
                            BytesProcessed: bytesProcessed,
                            TotalBytes: _streamLength,
                            MessagesProcessed: _messagesProcessed,
                            PercentComplete: percentComplete
                        ));
                    }

                    yield return message;
                }
                finally
                {
                    chunk.Dispose();
                }
            }
            else
            {
                yield break;
            }
        }
    }

    private async ValueTask<MessageChunk?> GetNextChunk(CancellationToken token)
    {
        return await MessageChunkReader.ReadAsync(_stream, token);
    }

    /// <summary>
    /// Disposes the player resources.
    /// The stream is owned by the caller and will not be disposed.
    /// Multiple calls to Dispose are safe (idempotent).
    /// </summary>
    public void Dispose()
    {
        // Stream is owned by the caller - we don't dispose it
        // This follows .NET conventions where the creator of a resource is responsible for its disposal
    }
}
