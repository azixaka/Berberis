using System.Buffers;

namespace Berberis.Recorder;

/// <summary>Message body serializer interface.</summary>
public interface IMessageBodySerializer<T>
{
    /// <summary>Gets the serializer version.</summary>
    SerializerVersion Version { get; }

    /// <summary>Serializes a value to a buffer writer.</summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="writer">The buffer writer to write to.</param>
    void Serialize(T value, IBufferWriter<byte> writer);

    /// <summary>Deserializes a value from a byte span.</summary>
    /// <param name="data">The data to deserialize.</param>
    /// <returns>The deserialized value.</returns>
    T Deserialize(ReadOnlySpan<byte> data);
}
