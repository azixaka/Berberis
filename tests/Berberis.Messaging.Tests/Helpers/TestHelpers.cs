using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Berberis.Messaging;
using Berberis.Messaging.Statistics;
using Berberis.Recorder;
using System.Buffers;
using System.Text;

namespace Berberis.Messaging.Tests.Helpers;

public static class TestHelpers
{
    /// <summary>
    /// Creates a CrossBar instance for testing with null logger
    /// </summary>
    public static CrossBar CreateTestCrossBar()
    {
        return new CrossBar(NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Waits for a condition to be true with timeout
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? checkInterval = null)
    {
        checkInterval ??= TimeSpan.FromMilliseconds(10);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(checkInterval.Value);
        }

        return condition();
    }

    /// <summary>
    /// Creates a test message with default values
    /// </summary>
    public static Message<T> CreateTestMessage<T>(
        T body,
        string? key = null,
        long correlationId = 0,
        string? from = null,
        long tagA = 0)
    {
        return new Message<T>(
            id: -1,
            timestamp: DateTime.UtcNow.ToBinary(),
            messageType: MessageType.ChannelUpdate,
            correlationId: correlationId,
            key: key,
            inceptionTicks: 0,
            from: from,
            body: body!,
            tagA: tagA
        );
    }

}

/// <summary>
/// Simple string serializer for testing recording/playback
/// Uses straightforward UTF-8 encoding without additional framing
/// </summary>
public class TestStringSerializer : IMessageBodySerializer<string>
{
    public SerializerVersion Version => new SerializerVersion(1, 0);

    public void Serialize(string value, IBufferWriter<byte> writer)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var bytes = Encoding.UTF8.GetBytes(value);
        var span = writer.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        writer.Advance(bytes.Length);
    }

    public string Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(data);
    }
}
