using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Idp.Bootstrappers;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Managers.ExceptionManagers;
using Booster.Auth.Idp.Managers.KeyManagers;
using Booster.Auth.Idp.Managers.TokenManagers;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Local;
using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Caching.Redis;
using Booster.Caching.Redis.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.Database.Postgres.Extensions;
using Booster.Database.Postgres.Settings;
using Booster.Database.Repositories;
using Booster.Hashing.Extensions;
using Booster.Hashing.Options;

namespace Booster.Auth.Idp.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddIdpServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettings<IdpOptions>(configuration);
        services.AddSettings<IdpUiOptions>(configuration);
        services.AddSettings<BootstrapOptions>(configuration);

        var postgresSettings = services.AddSettings<PostgresSettings>(configuration);
        var redisOptions = services.AddSettings<RedisCacheOptions>(configuration);

        var hashingOptions = services.AddSettings<HashingOptions>(configuration);

        services.AddSingleton(hashingOptions);

        services.AddPostgresDbContext<IdpDbContext>(postgresSettings);
        services.AddScoped(typeof(IAsyncRepository<,>), typeof(RepositoryBase<,>));

        services.AddHashing(hashingOptions);

        services.AddScoped<OidcTokenService>();
        services.AddSingleton<OidcKeyService>();

        services.AddScoped<IClientDataStore, MockClientDataStore>();
        services.AddScoped<ILocalUserStore, PrincipalStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ISessionCookieManager, SessionCookieManager>();

        services.AddScoped<IIdentityProvider, LocalProvider>();
        services.AddScoped<IProviderFactory, ProviderFactory>();

        services.AddScoped<AdminBootstrapper>();

        services.AddExceptionHandler<IdpExceptionHandler>();
        services.AddBoosterRedisCache(redisOptions);

        return services;
    }
}
