using Berberis.Messaging.Recorder;

namespace Berberis.Recorder.Cli.Utilities;

/// <summary>
/// Verifies recording integrity by attempting to read all messages.
/// </summary>
internal static class RecordingVerifier
{
    public static async Task VerifyAsync(Stream stream)
    {
        long messageCount = 0;
        long totalBytes = 0;

        while (true)
        {
            var startPosition = stream.Position;

            try
            {
                var chunk = await MessageChunkReader.ReadAsync(stream);
                if (chunk == null)
                    break;

                messageCount++;
                totalBytes += (stream.Position - startPosition);

                if (messageCount % 10000 == 0)
                {
                    Console.Write($"\rVerified {messageCount:N0} messages...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\rCorruption detected at message #{messageCount + 1}, byte offset {startPosition}");
                throw new InvalidDataException($"Recording corrupted: {ex.Message}", ex);
            }
        }

        Console.WriteLine($"\rVerified {messageCount:N0} messages successfully ({totalBytes:N0} bytes)");
    }
}
