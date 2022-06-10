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
                .AddHostedService<StatefulProducerService>()
                .AddHostedService<StatefulConsumerService>()
                //.AddHostedService<MonitoringService>()
                //.AddHostedService<MaxProducerService>()
                //.AddHostedService<MaxConsumerService>();
                //.AddHostedService<ProcessesProducerService>()
                //.AddHostedService<TimeProducerService>()
                //.AddHostedService<TimeConsumerService>()
                //.AddHostedService<ProcessesConsumerService1>()
                //.AddHostedService<ProcessesConsumerService2>();
                ;
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
    }
}
