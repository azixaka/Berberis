using System.Buffers.Binary;
using System.Buffers;
using System.IO;
using System.Text;

namespace Berberis.Recorder;

/// <summary>
/// Provides binary encoding/decoding utilities for recording operations.
/// </summary>
public static class BinaryCodec
{
    /// <summary>
    /// Writes a string to a buffer writer with length prefix.
    /// </summary>
    /// <param name="value">The string to write (null writes as zero-length).</param>
    /// <param name="bufferWriter">The buffer writer to write to.</param>
    public static void WriteString(string? value, IBufferWriter<byte> bufferWriter)
    {
        var lenLocation = bufferWriter.GetSpan(4);
        bufferWriter.Advance(4);
        var length = string.IsNullOrEmpty(value) ? 0 : (int)Encoding.UTF8.GetBytes(value, bufferWriter);
        BinaryPrimitives.WriteInt32LittleEndian(lenLocation, length);
    }

    /// <summary>
    /// Reads a length-prefixed string from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <returns>The decoded string, or null if length is zero.</returns>
    /// <exception cref="InvalidDataException">Thrown when the buffer contains corrupted data (length prefix exceeds available bytes).</exception>
    public static string? ReadString(ReadOnlySpan<byte> buffer)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(buffer);

        if (length == 0)
            return null;

        if (buffer.Length >= length + 4)
        {
            return Encoding.UTF8.GetString(buffer.Slice(4, length));
        }

        throw new InvalidDataException($"Corrupted message data: buffer length [{buffer.Length - 4}] is less than the string length prefix [{length}]");
    }
}
