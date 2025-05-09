using Serilog;
using System.Reflection;

namespace Berberis.SampleApp;

public static class Program
{
    public static void Main(string[] args)
    {
        var serviceEnvironment = new ServiceEnvironment("Trayport Service App", args);

        var host = Host.CreateDefaultBuilder(serviceEnvironment.WebHostArgs)
            .UseContentRoot(serviceEnvironment.PathToContentRoot)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;

                if (env.IsDevelopment())
                {
                    config.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
                }

                config
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(serviceEnvironment.WebHostArgs);
            })
            .UseSerilog((context, loggerConfiguration) =>
                loggerConfiguration.ReadFrom.Configuration(context.Configuration))
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseKestrel((context, options) =>
                    {
                        options.AllowSynchronousIO = true;
                        options.Configure(context.Configuration.GetSection("Kestrel"));
                    })
                    .UseStartup<Startup>();
            })
            .Build();

        host.Run();
    }
}
