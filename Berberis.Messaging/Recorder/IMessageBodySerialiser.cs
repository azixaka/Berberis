using System.Buffers;

namespace Berberis.Recorder;

public interface IMessageBodySerializer<T>
{
    SerializerVersion Version { get; }
    void Serialize(T value, IBufferWriter<byte> writer);
    T Deserialize(ReadOnlySpan<byte> data);
}
