using Berberis.Recorder;
using FluentAssertions;
using System.Buffers;
using Xunit;

namespace Berberis.Messaging.Tests.Recording;

public class BinaryCodecTests
{
    [Fact]
    public void ReadString_ValidData_ReturnsString()
    {
        // Arrange
        var writer = new ArrayBufferWriter<byte>();
        BinaryCodec.WriteString("Hello World", writer);
        var buffer = writer.WrittenSpan;

        // Act
        var result = BinaryCodec.ReadString(buffer);

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void ReadString_NullString_ReturnsNull()
    {
        // Arrange
        var writer = new ArrayBufferWriter<byte>();
        BinaryCodec.WriteString(null, writer);
        var buffer = writer.WrittenSpan;

        // Act
        var result = BinaryCodec.ReadString(buffer);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadString_EmptyString_ReturnsNull()
    {
        // Arrange
        var writer = new ArrayBufferWriter<byte>();
        BinaryCodec.WriteString("", writer);
        var buffer = writer.WrittenSpan;

        // Act
        var result = BinaryCodec.ReadString(buffer);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadString_InvalidLength_ThrowsInvalidDataException()
    {
        // VALIDATES: Task 6 - Correct exception type for corrupted data
        // VALIDATES: InvalidDataException thrown when length prefix exceeds buffer size
        // IMPACT: Proper error handling for corrupted recording data

        // Arrange - Create buffer with length prefix that exceeds actual buffer size
        var buffer = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(), 100); // Claim 100 bytes, but only have 4

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => BinaryCodec.ReadString(buffer));
        exception.Message.Should().Contain("Corrupted message data");
        exception.Message.Should().Contain("buffer length [4]");
        exception.Message.Should().Contain("string length prefix [100]");
    }

    [Fact]
    public void WriteString_NullAndEmpty_BothWriteZeroLength()
    {
        // VALIDATES: Null and empty strings both write as zero-length

        // Arrange
        var writerNull = new ArrayBufferWriter<byte>();
        var writerEmpty = new ArrayBufferWriter<byte>();

        // Act
        BinaryCodec.WriteString(null, writerNull);
        BinaryCodec.WriteString("", writerEmpty);

        // Assert
        writerNull.WrittenSpan.ToArray().Should().Equal(writerEmpty.WrittenSpan.ToArray());
        writerNull.WrittenSpan.Length.Should().Be(4); // Just the length prefix (0)
    }

}
