using Berberis.Recorder.Cli.Utilities;
using System.CommandLine;

namespace Berberis.Recorder.Cli.Commands;

internal static class BuildIndexCommand
{
    public static Command Create()
    {
        var command = new Command("build-index", "Build an index for fast seeking in a recording");
        var recordingArg = new Argument<string>("recording", "Path to the recording file");
        var indexArg = new Argument<string>("index", "Path to the output index file");
        var intervalOption = new Option<int>(
            "--interval",
            getDefaultValue: () => 1000,
            description: "Index every Nth message (default: 1000)");

        command.AddArgument(recordingArg);
        command.AddArgument(indexArg);
        command.AddOption(intervalOption);

        command.SetHandler(async (string recording, string index, int interval) =>
        {
            try
            {
                Console.WriteLine($"Building index for: {recording}");
                Console.WriteLine($"Index file: {index}");
                Console.WriteLine($"Interval: {interval} messages");
                Console.WriteLine();

                await using var recordingStream = File.OpenRead(recording);
                await using var indexStream = File.Create(index);

                await TypeAgnosticIndexBuilder.BuildAsync(recordingStream, indexStream, interval);

                Console.WriteLine();
                Console.WriteLine("Index built successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }, recordingArg, indexArg, intervalOption);

        return command;
    }
}
