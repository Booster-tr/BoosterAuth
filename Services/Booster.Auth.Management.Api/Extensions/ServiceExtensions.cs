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
        services.AddScoped<WalkToWinBootstrapper>();
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
