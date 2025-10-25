using Berberis.Recorder;
using System.Buffers;
using System.Buffers.Binary;

namespace Berberis.Messaging.Recorder;

/// <summary>
/// Utility for reading MessageChunk from streams without requiring generic type knowledge.
/// Used by Player and CLI tools.
/// </summary>
internal static class MessageChunkReader
{
    /// <summary>
    /// Reads the next message chunk from a stream, or null if end of stream.
    /// </summary>
    public static async ValueTask<MessageChunk?> ReadAsync(Stream stream, CancellationToken token = default)
    {
        // Rent small header buffer from pool (eliminates 28-byte allocation per message)
        var headerBuffer = ArrayPool<byte>.Shared.Rent(MessageCodec.HeaderSize);

        try
        {
            var rcvdCnt = await stream.ReadAsync(headerBuffer.AsMemory(0, MessageCodec.HeaderSize), token);

            if (rcvdCnt == 0)
                return null;

            while (rcvdCnt < MessageCodec.HeaderSize)
            {
                var rcvdBytes = await stream.ReadAsync(headerBuffer, rcvdCnt, MessageCodec.HeaderSize - rcvdCnt, token);

                if (rcvdBytes == 0)
                    return null;

                rcvdCnt += rcvdBytes;
            }

            var totalMsgLen = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);

            var buffer = ArrayPool<byte>.Shared.Rent(totalMsgLen);

            try
            {
                var bufferMemory = buffer.AsMemory();

                headerBuffer.AsSpan(0, MessageCodec.HeaderSize).CopyTo(bufferMemory.Span);

                var rcvdBytes = await stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt, token);

                if (rcvdBytes == 0)
                    return null;

                rcvdCnt += rcvdBytes;

                while (rcvdCnt < totalMsgLen)
                {
                    rcvdBytes = await stream.ReadAsync(buffer, rcvdCnt, totalMsgLen - rcvdCnt, token);

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
                throw;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
        }
    }
}
