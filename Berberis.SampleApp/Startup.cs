using Berberis.Messaging;
using Serilog;

namespace Berberis.SampleApp;

public sealed class Startup
{
    public Startup(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        Configuration = configuration;
        HostEnvironment = hostEnvironment;

        Log.Information("Berberis Sample App is starting...");
    }

    public IConfiguration Configuration { get; }

    public IHostEnvironment HostEnvironment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions();

        services.AddSingleton<ICrossBar, CrossBar>()
                .AddHostedService<ChannelsMonitoringService>()
                .AddHostedService<ProcessesProducerService>()
                .AddHostedService<TimeProducerService>()
                .AddHostedService<ProcessesConsumerService1>()
                .AddHostedService<ProcessesConsumerService2>()
                .AddHostedService<TimeConsumerService>();
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
    }
}
