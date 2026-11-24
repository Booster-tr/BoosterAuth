using Booster.Auth.Z.Managers.QueryAccess;
using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Z.Bootstrappers;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Managers.LookupManagers;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.Auth.Management.Contracts;
using Booster.Auth.Management.Grpc;
using Booster.Licensing.Runtime;

namespace Booster.Auth.Z.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthZServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<AuthZDbContext>(configuration);
        services.AddBoosterMessaging<AuthZDbContext>(configuration);

        services.AddScoped<IPoolStoreProvider, PoolStoreProvider>();

        services.AddScoped<ILookupManager, LookupManager>();
        services.AddScoped<QueryCallerGuard>();

        services.AddScoped<ManagementBootstrapper>();
        services.AddScoped<LicensingBootstrapper>();

        // Lisans: panel yazma islemleri (iliski/sema) lisans kisitliyken reddedilir (LicenseGuard).
        // Adres verilmemisse kontrol yapilmaz; bu servis lisansin sahibi degildir.
        var managementGrpc = configuration["ManagementSettings:GrpcAddress"];
        if (!string.IsNullOrWhiteSpace(managementGrpc))
        {
            services.AddGrpcClient<LicenseStatusService.LicenseStatusServiceClient>(o => o.Address = new Uri(managementGrpc));
            services.AddSingleton<ILicenseStatusProvider>(sp => new GrpcLicenseStatusProvider(
                sp.GetRequiredService<LicenseStatusService.LicenseStatusServiceClient>()));
        }

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();

        return services;
    }
}
