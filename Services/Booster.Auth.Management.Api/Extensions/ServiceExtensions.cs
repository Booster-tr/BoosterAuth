using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;

namespace Booster.Auth.Management.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthNServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<AuthNDbContext>(configuration);
        services.AddSettings<BootstrapOptions>(configuration);

        services.AddScoped<ManagementBootstrapper>();
        services.AddScoped<IClientDataCacheStore, ClientDataCacheStore>();

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();


        return services;
    }
}
