using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;

namespace Berberis.Recorder;

public sealed class Player<TBody> : IPlayer
{
    private ICrossBar _crossBar;
    private string _channel;
    private Stream _stream;
    private IMessageBodySerializer<TBody> _serialiser;

    private Player() { }

    private void Initialise(ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser)
    {
        _crossBar = crossBar;
        _channel = channel;
        _stream = stream;
        _serialiser = serialiser;
    }

    internal static IPlayer CreatePlayer(ICrossBar crossBar, string channel, Stream stream,
        IMessageBodySerializer<TBody> serialiser, PlayMode playMode, CancellationToken token)
    {
        var player = new Player<TBody>();
        player.Initialise(crossBar, channel, stream, serialiser);
        return player;
    }

    public async Task Play()
    {
        while (_stream.Position < _stream.Length)
        {
            var headerBuffer = new byte[MessageCodec.HeaderSize];
            var rcvdCnt = await _stream.ReadAsync(headerBuffer);

            while (rcvdCnt < MessageCodec.HeaderSize)
            {
                rcvdCnt += await _stream.ReadAsync(headerBuffer, rcvdCnt, MessageCodec.HeaderSize - rcvdCnt);
            }

            var totalMsgLen = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);

            var buffer = ArrayPool<byte>.Shared.Rent(totalMsgLen);

            try
            {
                var bufferMemory = buffer.AsMemory();

                headerBuffer.AsSpan().CopyTo(bufferMemory.Span);

                rcvdCnt += await _stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt);

                while (rcvdCnt < totalMsgLen)
                {
                    rcvdCnt += await _stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt);
                }

                // skip total message length
                var bodyOffset = BinaryPrimitives.ReadUInt16LittleEndian(bufferMemory.Slice(4).Span);

                bufferMemory = bufferMemory.Slice(MessageCodec.HeaderSize); // skip header

                var key = BinaryCodec.ReadString(bufferMemory.Span);
                bufferMemory = buffer.AsMemory().Slice(bodyOffset);

                var obj = _serialiser.Derialise(bufferMemory.Span);

                await _crossBar.Publish($"{_channel}{key}", obj, key, false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public bool Pause()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}
