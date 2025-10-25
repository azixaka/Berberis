using Berberis.Messaging.Recorder;

namespace Berberis.Recorder.Cli.Utilities;

/// <summary>
/// Builds recording indexes without requiring knowledge of the message body type.
/// Works directly with the binary format using MessageCodec.
/// </summary>
internal static class TypeAgnosticIndexBuilder
{
    public static async Task BuildAsync(Stream recordingStream, Stream indexStream, int interval)
    {
        // Write placeholder header (will update at end)
        var magic = System.Text.Encoding.ASCII.GetBytes("RIDX");
        await indexStream.WriteAsync(magic);
        await indexStream.WriteAsync(BitConverter.GetBytes((ushort)1)); // version
        await indexStream.WriteAsync(BitConverter.GetBytes(interval));
        await indexStream.WriteAsync(BitConverter.GetBytes((long)0)); // placeholder for count

        long messageNumber = 0;
        long indexedCount = 0;
        var progress = new Progress<int>(percent =>
        {
            if (percent % 10 == 0)
                Console.Write($"\rProgress: {percent}%");
        });

        while (true)
        {
            var startPosition = recordingStream.Position;
            var chunk = await MessageChunkReader.ReadAsync(recordingStream);
            if (chunk == null)
                break;

            if (messageNumber % interval == 0)
            {
                // Write index entry
                await indexStream.WriteAsync(BitConverter.GetBytes(messageNumber));
                await indexStream.WriteAsync(BitConverter.GetBytes(startPosition));
                await indexStream.WriteAsync(BitConverter.GetBytes(chunk.Value.TimestampTicks));
                indexedCount++;

                // Report progress
                if (recordingStream.Length > 0)
                {
                    var percent = (int)((recordingStream.Position * 100) / recordingStream.Length);
                    ((IProgress<int>)progress).Report(percent);
                }
            }

            messageNumber++;
        }

        Console.WriteLine($"\rProgress: 100%");
        Console.WriteLine($"Indexed {indexedCount:N0} entries from {messageNumber:N0} messages");

        // Update header with actual count
        indexStream.Position = magic.Length + sizeof(ushort) + sizeof(int);
        await indexStream.WriteAsync(BitConverter.GetBytes(indexedCount));
    }
}
