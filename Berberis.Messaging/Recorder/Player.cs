using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Berberis.Recorder;

/// <summary>
/// Plays back recorded messages from a stream.
/// </summary>
/// <typeparam name="TBody">The message body type.</typeparam>
public sealed partial class Player<TBody> : IPlayer<TBody>
{
    private Stream _stream;
    private IMessageBodySerializer<TBody> _serialiser;
    private PlayMode _playMode;
    private readonly RecorderStatsReporter _recorderStatsReporter = new();

    private Player(Stream stream, IMessageBodySerializer<TBody> serialiser, PlayMode playMode)
    {
        _stream = stream;
        _serialiser = serialiser;
        _playMode = playMode;
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
           new Player<TBody>(stream, serialiser, playMode);

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

                    var message = new Message<TBody>(chunk.Id, chunk.Timestamp, chunk.Type, 0, chunk.Key, 0, chunk.From, obj, 0);

                    _recorderStatsReporter.Stop(ticks, chunk.Length);

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
        var headerBuffer = new byte[MessageCodec.HeaderSize];
        var rcvdCnt = await _stream.ReadAsync(headerBuffer);

        if (rcvdCnt == 0)
            return null;

        while (rcvdCnt < MessageCodec.HeaderSize)
        {
            var rcvdBytes = await _stream.ReadAsync(headerBuffer, rcvdCnt, MessageCodec.HeaderSize - rcvdCnt);

            if (rcvdBytes == 0)
                return null;

            rcvdCnt += rcvdBytes;
        }

        var totalMsgLen = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);

        var buffer = ArrayPool<byte>.Shared.Rent(totalMsgLen);

        try
        {
            var bufferMemory = buffer.AsMemory();

            headerBuffer.AsSpan().CopyTo(bufferMemory.Span);

            var rcvdBytes = await _stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt);

            if (rcvdBytes == 0)
                return null;

            rcvdCnt += rcvdBytes;

            while (rcvdCnt < totalMsgLen)
            {
                rcvdBytes = await _stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt);

                if (rcvdBytes == 0)
                    return null;

                rcvdCnt += rcvdBytes;
            }

            var msgChunk = new MessageChunk(buffer, totalMsgLen);

            return msgChunk;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return null;
    }

    /// <summary>
    /// Disposes the player resources.
    /// </summary>
    public void Dispose()
    {
    }
}
