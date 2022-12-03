using Berberis.Recorder;
using System.Buffers.Binary;
using System.Buffers;

namespace Berberis.SampleApp;

public sealed class NumberSerialiser : IMessageBodySerializer<long>
{
    public SerializerVersion Version { get; } = new SerializerVersion(1, 0);

    public long Deserialize(ReadOnlySpan<byte> data)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(data);
    }

    public void Serialize(long value, IBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteInt64LittleEndian(span, value);
        writer.Advance(8);
    }
}
