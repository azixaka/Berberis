using System.Buffers;

namespace Berberis.Recorder;

public interface IMessageBodySerializer<T>
{
    SerializerVersion Version { get; }
    void Serialise(T value, IBufferWriter<byte> writer);
    T Derialise(ReadOnlySpan<byte> data);
}
