using Booster.Authorization.Relational.Fga;
using Booster.Authorization.Relational.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Authorization.Relational.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddBoosterAuthZ(this IServiceCollection services, FgaOptions fgaOptions)
    {
        services.AddSingleton<IFgaClientProvider, FgaClientProvider>();

        services.AddSingleton<FgaSchemaParser>();
        services.AddScoped<FgaSchemaWriter>();
        services.AddScoped<FgaRelationManager>();
        return services;
    }

}
