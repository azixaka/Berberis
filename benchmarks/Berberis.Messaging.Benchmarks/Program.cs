using System;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;

namespace Berberis.Messaging.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Check for quick mode (--quick flag for fast baseline run)
        bool quickMode = args.Any(a => a.Equals("--quick", StringComparison.OrdinalIgnoreCase));

        // Remove --quick from args before passing to BenchmarkDotNet (it doesn't recognize it)
        var filteredArgs = args.Where(a => !a.Equals("--quick", StringComparison.OrdinalIgnoreCase)).ToArray();

        IConfig config;

        if (quickMode)
        {
            // QUICK MODE: Minimal iterations for fast baseline (~2-3 minutes for all 60 benchmarks)
            // Use this for: initial development, quick smoke tests, rough performance checks
            // NOT for: production baselines, regression detection, official results
            Console.WriteLine("========================================");
            Console.WriteLine("QUICK MODE: Running minimal iterations");
            Console.WriteLine("- 1 warmup iteration");
            Console.WriteLine("- 3 actual iterations");
            Console.WriteLine("- Results will be APPROXIMATE only");
            Console.WriteLine("========================================");
            Console.WriteLine();

            var quickJob = Job.Default
                .WithWarmupCount(1)        // Just 1 warmup instead of 6-15
                .WithIterationCount(3)     // Just 3 iterations instead of 15-100
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithGcForce(false);

            config = DefaultConfig.Instance
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddColumn(StatisticColumn.P50)
                .AddColumn(StatisticColumn.P90)
                .AddColumn(StatisticColumn.P95)
                .AddJob(quickJob);
        }
        else
        {
            // STANDARD MODE: Full statistical analysis (~20-30 minutes for all 60 benchmarks)
            // Use this for: official baselines, regression detection, production metrics
            config = DefaultConfig.Instance
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddColumn(StatisticColumn.P50)
                .AddColumn(StatisticColumn.P90)
                .AddColumn(StatisticColumn.P95)
                .AddJob(Job.Default
                    .WithGcServer(true)
                    .WithGcConcurrent(true)
                    .WithGcForce(false));
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(filteredArgs, config);
    }
}
