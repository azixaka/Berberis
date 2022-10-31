using System.Buffers.Binary;
using System.Buffers;
using System.Text;

namespace Berberis.Recorder;

public static class BinaryCodec
{
    public static void WriteString(string? value, IBufferWriter<byte> bufferWriter)
    {
        var lenLocation = bufferWriter.GetSpan(4);
        bufferWriter.Advance(4);
        var length = string.IsNullOrEmpty(value) ? 0 : (int)Encoding.UTF8.GetBytes(value, bufferWriter);
        BinaryPrimitives.WriteInt32LittleEndian(lenLocation, length);
    }

    public static string? ReadString(ReadOnlySpan<byte> buffer)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(buffer);

        if (length == 0)
            return null;

        if (buffer.Length >= length + 4)
        {
            return Encoding.UTF8.GetString(buffer.Slice(4, length));
        }

        throw new IndexOutOfRangeException($"Buffer length [{buffer.Length - 4}] is less than the string length [{length}]");
    }
}
