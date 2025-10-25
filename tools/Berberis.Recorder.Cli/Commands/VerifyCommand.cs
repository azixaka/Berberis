using Berberis.Recorder.Cli.Utilities;
using System.CommandLine;

namespace Berberis.Recorder.Cli.Commands;

internal static class VerifyCommand
{
    public static Command Create()
    {
        var command = new Command("verify", "Verify recording integrity (check for corruption)");
        var recordingArg = new Argument<string>("recording", "Path to the recording file");
        command.AddArgument(recordingArg);

        command.SetHandler(async (string recording) =>
        {
            try
            {
                Console.WriteLine($"Verifying recording: {recording}");
                Console.WriteLine();

                await using var stream = File.OpenRead(recording);
                await RecordingVerifier.VerifyAsync(stream);

                Console.WriteLine();
                Console.WriteLine("Recording is valid!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }, recordingArg);

        return command;
    }
}
