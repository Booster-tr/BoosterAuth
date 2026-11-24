using Booster.Auth.Common.Internal.Amqp.Extensions;
using Booster.Auth.Common.Internal.Extensions;
using Booster.Hashing.Extensions;
using Booster.Hashing.Options;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Auth.Management.Api.Managers.PrincipalLookupManagers;
using Booster.Auth.Management.Api.Options;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.Auth.Management.Api.Licensing;
using Booster.Licensing.Runtime;
using Booster.Licensing.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Booster.Auth.Management.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthNServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<AuthNDbContext>(configuration);
        services.AddBoosterMessaging<AuthNDbContext>(
            configuration,
            configureRider: rider => rider.AddPoolDefinitionProducers());
        services.AddSettings<BootstrapOptions>(configuration);

        var hashingOptions = services.AddSettings<HashingOptions>(configuration);
        services.AddHashing(hashingOptions);

        services.AddScoped<ManagementBootstrapper>();
        services.AddScoped<LicensingBootstrapper>();

        // Lisans: sahibi bu servis. Dogrulama tamamen cevrimdisi; yalnizca koda gomulu public key'ler.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<Booster.Auth.Ldap.LdapDirectory>();
        services.AddSingleton<ILicenseValidator, LicenseValidator>();
        services.AddScoped<InstallationStore>();
        services.AddSingleton<LicenseService>();
        services.AddSingleton<ILicenseStatusProvider>(sp => sp.GetRequiredService<LicenseService>());
        services.AddHostedService<LicenseMonitor>();
        services.AddScoped<Booster.Auth.Common.Internal.Licensing.ILicenseLimitService, Booster.Auth.Common.Internal.Licensing.LicenseLimitService>();
        services.AddScoped<IClientDataCacheStore, ClientDataCacheStore>();
        services.AddScoped<IAccessManager, AccessManager>();
        services.AddScoped<IPrincipalLookupManager, PrincipalLookupManager>();

        var idpSettings = services.AddSettings<IdpSettings>(configuration);

        services.AddGrpcClient<PrincipalLookupService.PrincipalLookupServiceClient>(options =>
        {
            options.Address = new Uri(idpSettings.GrpcAddress);
        })
        .ConfigureChannel(channel =>
        {
            channel.HttpHandler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(idpSettings.TimeoutSeconds),
                EnableMultipleHttp2Connections = true
            };
        });

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();


        return services;
    }
}
