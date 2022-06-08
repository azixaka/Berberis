using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Berberis.SampleApp;

public sealed class ServiceEnvironment
{
    private const string ServiceArg = "--windows-service";

    private readonly string _serviceName;

    public ServiceEnvironment(string serviceName, string[] commandLineArgs)
    {
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        Parse(commandLineArgs);
    }

    public string? PathToContentRoot { get; private set; }

    public string[]? WebHostArgs { get; private set; }

    public bool IsService { get; private set; }

    public void Parse(string[] commandLineArgs)
    {
        IsService = !Debugger.IsAttached &&
                    commandLineArgs.Contains(ServiceArg) &&
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (IsService)
        {
            PathToContentRoot = AppContext.BaseDirectory;
            WebHostArgs = commandLineArgs.Where(arg => arg != ServiceArg).ToArray();
        }
        else
        {
            Console.Title = _serviceName;
            Console.WriteLine(_serviceName);

            foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
            {
                Console.WriteLine($"{pair.Key}={pair.Value}");
            }

            PathToContentRoot = Directory.GetCurrentDirectory();
            WebHostArgs = commandLineArgs;
        }
    }
}