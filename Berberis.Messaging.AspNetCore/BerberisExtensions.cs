using Microsoft.Extensions.DependencyInjection;

namespace Berberis.Messaging.AspNetCore;

public static class BerberisExtensions
{
    public static IServiceCollection AddBerberisConsumer<T>(this IServiceCollection services)
        where T : class, IBerberisConsumer
    {
        services.AddSingleton<T>();
        services.AddSingleton<IBerberisConsumer, T>(sp => sp.GetService<T>()!);
        return services;
    }

    public static IServiceCollection AddBerberisConsumerHostedService(this IServiceCollection services)
    {
        services.AddHostedService<BerberisConsumerBackgroundService>();
        return services;
    }
}