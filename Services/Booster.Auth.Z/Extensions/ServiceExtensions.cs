using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Z.Bootstrappers;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Managers.LookupManagers;
using Booster.Auth.Z.Stores.PoolStores;

namespace Booster.Auth.Z.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthZServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<AuthZDbContext>(configuration);

        services.AddScoped<IPoolStoreProvider, PoolStoreProvider>();

        services.AddScoped<ILookupManager, LookupManager>();

        services.AddScoped<ManagementBootstrapper>();

        return services;
    }
}
