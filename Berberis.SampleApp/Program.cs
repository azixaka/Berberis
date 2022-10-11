using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Serilog;
using System.Reflection;

namespace Berberis.SampleApp;

public static class Program
{
    public static void Main(string[] args)
    {
        var serviceEnvironment = new ServiceEnvironment("Trayport Service App", args);

        var webHost = WebHost.CreateDefaultBuilder(serviceEnvironment.WebHostArgs!)
            .UseKestrel()

            .ConfigureKestrel(
                (context, options) =>
                {
                    options.AllowSynchronousIO = true;
                    options.Configure(context.Configuration.GetSection("Kestrel"));
                })
            .UseContentRoot(serviceEnvironment.PathToContentRoot!)
            .ConfigureAppConfiguration(
                (hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    if (env.IsDevelopment())
                    {
                        config.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                    }

                    config
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .AddCommandLine(serviceEnvironment.WebHostArgs); // and here to allow overriding settings values
                })
            .UseSerilog((context, loggerConfiguration) =>
                loggerConfiguration.ReadFrom.Configuration(context.Configuration))
           .UseStartup<Startup>()
           .Build();

        if (serviceEnvironment.IsService)
        {
            webHost.RunAsService();
        }
        else
        {
            webHost.Run();
        }
    }
}

