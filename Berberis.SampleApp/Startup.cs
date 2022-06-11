using Berberis.Messaging;
using Berberis.Messaging.AspNetCore;
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
                .AddHostedService<MonitoringService>()
                .AddHostedService<StockPriceProducerService>()
                .AddHostedService<StockPriceConsumerService>()
                //.AddHostedService<StatefulProducerService>()
                //.AddHostedService<StatefulConsumerService>()
                //.AddHostedService<MaxProducerService>()
                //.AddHostedService<MaxConsumerService>()
                //.AddHostedService<ProcessesProducerService>()
                //.AddHostedService<TimeProducerService>()
                //.AddHostedService<TimeConsumerService>()
                //.AddHostedService<ProcessesConsumerService1>()
                //.AddHostedService<ProcessesConsumerService2>();
                ;

        services.AddBerberisConsumerHostedService()
                .AddBerberisConsumer<StockPriceConsumer>()
                //.AddBerberisConsumer<ProcessesConsumer>()
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
