using Booster.Auth.Common.Internal.Extensions;
using Booster.Hashing.Extensions;
using Booster.Hashing.Options;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
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
        services.AddBoosterMessaging<AuthNDbContext>(configuration);
        services.AddSettings<BootstrapOptions>(configuration);

        var hashingOptions = services.AddSettings<HashingOptions>(configuration);
        services.AddHashing(hashingOptions);

        services.AddScoped<ManagementBootstrapper>();
        services.AddScoped<IClientDataCacheStore, ClientDataCacheStore>();
        services.AddScoped<IAccessManager, AccessManager>();

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();


        return services;
    }
}
