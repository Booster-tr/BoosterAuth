using Booster.Logging.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Logging.BoosterAmqp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAmqpLogger(this IServiceCollection services)
    {
        services.AddScoped<AmqpLogWriter>();
        services.AddScoped<ILogWriter>(sp => sp.GetRequiredService<AmqpLogWriter>());
        services.AddScoped<ILogPublisher>(sp => sp.GetRequiredService<AmqpLogWriter>());
        return services;
    }
}
