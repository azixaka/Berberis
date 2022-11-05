using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;

namespace Berberis.Recorder;

public sealed partial class Player<TBody>
{
    internal readonly struct MessageChunk
    {
        private readonly byte[] _data;

        public MessageChunk(byte[] data, int length)
        {
            _data = data;
            Length = length;
        }

        public int Length { get; }

        public ushort BodyOffset => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan().Slice(4));

        public MessageType Type => (MessageType) _data.AsSpan()[6];

        public byte MessageVersion => _data.AsSpan()[7];

        public Span<byte> Options => _data.AsSpan().Slice(8, 4);

        public long Id => BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan().Slice(12));

        public long Timestamp => BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan().Slice(20));

        public string? Key => BinaryCodec.ReadString(_data.AsSpan().Slice(MessageCodec.HeaderSize));
        
        public string? From
        {
            get
            {
                var keyLen = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan().Slice(MessageCodec.HeaderSize));

                if (keyLen == 0)
                    return null;

                return BinaryCodec.ReadString(_data.AsSpan().Slice(MessageCodec.HeaderSize + 4 + keyLen));
            }
        }

        public Span<byte> Body => _data.AsSpan().Slice(BodyOffset, Length - BodyOffset - 4);

        public void Dispose()
        { 
            ArrayPool<byte>.Shared.Return(_data);
        }
    }
}
