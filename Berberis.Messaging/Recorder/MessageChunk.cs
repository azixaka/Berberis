using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.Buffers;
using System.Buffers.Binary;

namespace Berberis.Recorder;

/// <summary>
/// Internal representation of a recorded message chunk containing raw message data and cached parsed properties.
/// </summary>
/// <remarks>
/// This struct caches the Key and From properties after first access to avoid redundant string allocations
/// and parsing overhead. Caching is not thread-safe but is acceptable since MessageChunk instances are
/// short-lived and accessed from a single thread during playback.
/// </remarks>
internal struct MessageChunk
{
    private readonly byte[] _data;
    private string? _cachedKey;   // Lazy-initialized cache for Key property
    private string? _cachedFrom;  // Lazy-initialized cache for From property

    public MessageChunk(byte[] data, int length)
    {
        _data = data;
        Length = length;
        _cachedKey = null;
        _cachedFrom = null;
    }

    public int Length { get; }

    public ushort BodyOffset => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan().Slice(4));

    public MessageType Type => (MessageType)_data.AsSpan()[6];

    public byte MessageVersion => _data.AsSpan()[7];

    public Span<byte> Options => _data.AsSpan().Slice(8, 4);

    public long Id => BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan().Slice(12));

    public long TimestampTicks => BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan().Slice(20));

    public string? Key => _cachedKey ??= BinaryCodec.ReadString(_data.AsSpan().Slice(MessageCodec.HeaderSize));

    public string? From
    {
        get
        {
            if (_cachedFrom is not null)
                return _cachedFrom;

            var keyLen = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan().Slice(MessageCodec.HeaderSize));

            if (keyLen == 0)
                return null;

            return _cachedFrom = BinaryCodec.ReadString(_data.AsSpan().Slice(MessageCodec.HeaderSize + 4 + keyLen));
        }
    }

    public Span<byte> Body => _data.AsSpan().Slice(BodyOffset, Length - BodyOffset - 4);

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_data);
    }
}
