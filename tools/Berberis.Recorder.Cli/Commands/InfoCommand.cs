using Berberis.Recorder;
using Berberis.Recorder.Cli.Utilities;
using System.CommandLine;

namespace Berberis.Recorder.Cli.Commands;

internal static class InfoCommand
{
    public static Command Create()
    {
        var command = new Command("info", "Display information about a recording");
        var recordingArg = new Argument<string>("recording", "Path to the recording file");
        command.AddArgument(recordingArg);

        command.SetHandler(async (string recording) =>
        {
            try
            {
                await ShowInfo(recording);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }, recordingArg);

        return command;
    }

    private static async Task ShowInfo(string recording)
    {
        // Read metadata if available
        var metadataPath = RecordingMetadata.GetMetadataPath(recording);
        var metadata = await RecordingMetadata.ReadAsync(metadataPath);

        if (metadata != null)
        {
            Console.WriteLine($"Recording: {recording}");
            Console.WriteLine($"Created: {metadata.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Channel: {metadata.Channel}");
            Console.WriteLine($"Serializer: {metadata.SerializerType} v{metadata.SerializerVersion}");
            Console.WriteLine($"Message Type: {metadata.MessageType}");

            if (metadata.MessageCount.HasValue)
                Console.WriteLine($"Message Count: {metadata.MessageCount:N0}");

            if (metadata.DurationMs.HasValue)
                Console.WriteLine($"Duration: {TimeSpan.FromMilliseconds(metadata.DurationMs.Value)}");

            if (metadata.Custom != null && metadata.Custom.Count > 0)
            {
                Console.WriteLine("\nCustom metadata:");
                foreach (var kvp in metadata.Custom)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Recording: {recording}");
            Console.WriteLine("No metadata file found (.meta.json)");
            Console.WriteLine();

            // Show basic file info and try to read recording
            var fileInfo = new FileInfo(recording);
            if (fileInfo.Exists)
            {
                Console.WriteLine($"File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
                Console.WriteLine($"Last modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

                // Try to read without metadata - scan messages
                await RecordingScanner.ScanAndDisplay(recording);
            }
            else
            {
                Console.WriteLine("Error: Recording file not found");
            }
        }
    }
}
