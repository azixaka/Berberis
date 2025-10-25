using Berberis.Recorder;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Berberis.Messaging.Recorder;

/// <summary>
/// Handles encoding and decoding of message headers for the recorder system.
/// </summary>
/// <remarks>
/// Message Format (wire format):
/// - 4 bytes: Total message length (prefix)
/// - 2 bytes: Body offset
/// - 1 byte: Message type
/// - 1 byte: Message version
/// - 4 bytes: Options (bytes 0-1: reserved, bytes 2-3: serializer version major/minor)
/// - 8 bytes: Message ID
/// - 8 bytes: Timestamp
/// - Variable: Key (length-prefixed string)
/// - Variable: From (length-prefixed string)
/// - Variable: Message body
/// - 4 bytes: Total message length (suffix, for validation)
///
/// The suffix length serves as a message framing validator - if prefix != suffix,
/// the message data is corrupted.
/// </remarks>
internal static class MessageCodec
{
    /// <summary>
    /// Size of the fixed message header in bytes (before variable-length Key/From fields).
    /// </summary>
    public const int HeaderSize = 28;

    public static Span<byte> WriteChannelMessageHeader<TBody>(PipeWriter pipeWriter, SerializerVersion serializerVersion, ref Message<TBody> message)
    {
        // | 4 bytes | 2 bytes | 1 byte | 1 byte | 4 bytes | 8 bytes | 8 bytes | 4 bytes -> X bytes | 4 bytes -> Y bytes |
       
        // | Total Message Bytes Length
        // | Message Body Bytes Offset
        // | Message Type
        // | Message Type Version
        // | 4x bytes Options
        // | Message Id
        // | Timestamp
        // | Key
        // | From |

        //_writer.Write(message header)
        var messageLengthSpan = pipeWriter.GetSpan(HeaderSize);
        var bodyOffsetSpan = messageLengthSpan.Slice(4);

        var writeSpan = bodyOffsetSpan.Slice(2);
        writeSpan[0] = (byte) message.MessageType;
        writeSpan[1] = 1; // Message Version
        writeSpan[2] = 0; // Options 1
        writeSpan[3] = 0; // Options 2
        writeSpan[4] = serializerVersion.Major; // Options 3
        writeSpan[5] = serializerVersion.Minor; // Options 4
        writeSpan = writeSpan.Slice(6);

        BinaryPrimitives.WriteInt64LittleEndian(writeSpan, message.Id);
        writeSpan = writeSpan.Slice(8);
        BinaryPrimitives.WriteInt64LittleEndian(writeSpan, message.Timestamp);
        pipeWriter.Advance(HeaderSize);

        BinaryCodec.WriteString(message.Key, pipeWriter);
        BinaryCodec.WriteString(message.From, pipeWriter);

        // Write relative message body offset
        BinaryPrimitives.WriteUInt16LittleEndian(bodyOffsetSpan, (ushort)pipeWriter.UnflushedBytes);

        return messageLengthSpan;
    }

    public static void WriteMessageLengthPrefixAndSuffix(PipeWriter pipeWriter, Span<byte> prefixLength)
    {
        var totalMessageLength = (int)pipeWriter.UnflushedBytes + 4;

        // Write total message length to the very beginning and the very end of the message
        BinaryPrimitives.WriteInt32LittleEndian(prefixLength, totalMessageLength);
        BinaryPrimitives.WriteInt32LittleEndian(pipeWriter.GetSpan(4), totalMessageLength);
        pipeWriter.Advance(4);
    }
}
