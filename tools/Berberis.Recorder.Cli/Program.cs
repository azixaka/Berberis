using Berberis.Recorder.Cli.Commands;
using System.CommandLine;

namespace Berberis.Recorder.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Berberis Recorder CLI - Tools for working with message recordings");

        rootCommand.AddCommand(InfoCommand.Create());
        rootCommand.AddCommand(BuildIndexCommand.Create());
        rootCommand.AddCommand(VerifyCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
