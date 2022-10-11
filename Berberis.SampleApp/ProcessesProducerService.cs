using Berberis.Messaging;
using System.Diagnostics;

namespace Berberis.SampleApp;

public sealed class ProcessesProducerService : BackgroundService
{
    private readonly ICrossBar _xBar;

    public ProcessesProducerService(ICrossBar xBar)
    {
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000, stoppingToken);

        const string destination = "processes.info";

        var processes = Process.GetProcesses();

        var exceptions = new HashSet<int>();

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var process in processes)
            {
                try
                {
                    if (!exceptions.Contains(process.Id))
                    {
                        var pi = new ProcessInfo
                        {
                            Timestamp = DateTime.UtcNow,
                            ProcessId = process.Id,
                            Name = process.ProcessName,
                            CpuTimeMs = process.TotalProcessorTime.TotalMilliseconds
                        };

                        _ = _xBar.Publish(destination, pi, nameof(ProcessesProducerService));
                        await Task.Delay(250);
                    }
                }
                catch
                {
                    exceptions.Add(process.Id);
                }
            }
        }
    }
}
