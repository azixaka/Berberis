using Berberis.Messaging.Recorder;

namespace Berberis.Recorder.Cli.Utilities;

/// <summary>
/// Scans recordings without requiring type knowledge.
/// </summary>
internal static class RecordingScanner
{
    public static async Task ScanAndDisplay(string path)
    {
        Console.WriteLine();
        Console.WriteLine("Scanning recording (without metadata)...");

        await using var stream = File.OpenRead(path);
        var stats = await ScanAsync(stream);

        Console.WriteLine($"Message Count: {stats.MessageCount:N0}");
        if (stats.Duration.HasValue)
        {
            Console.WriteLine($"Duration: {stats.Duration.Value}");
        }
    }

    public static async Task<ScanResult> ScanAsync(Stream stream)
    {
        long messageCount = 0;
        long? firstTicks = null;
        long? lastTicks = null;

        while (true)
        {
            var chunk = await MessageChunkReader.ReadAsync(stream);
            if (chunk == null)
                break;

            messageCount++;
            lastTicks = chunk.Value.TimestampTicks;
            firstTicks ??= lastTicks;
        }

        TimeSpan? duration = null;
        if (firstTicks.HasValue && lastTicks.HasValue)
        {
            duration = TimeSpan.FromTicks(lastTicks.Value - firstTicks.Value);
        }

        return new ScanResult(messageCount, duration);
    }

    public record ScanResult(long MessageCount, TimeSpan? Duration);
}
