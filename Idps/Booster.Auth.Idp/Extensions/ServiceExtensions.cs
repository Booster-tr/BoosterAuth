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

namespace Booster.Auth.Idp.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddIdpServices(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddSettings<IdpOptions>(configuration);
        services.AddSettings<IdpUiOptions>(configuration);
        services.AddScoped<OidcTokenService>();
        var redisOptions = services.AddSettings<RedisCacheOptions>(configuration);
        services.AddSingleton<OidcKeyService>();

        services.AddScoped<IClientDataStore, MockClientDataStore>();
        services.AddScoped<ILocalUserStore, PrincipalStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ISessionCookieManager, SessionCookieManager>();

        services.AddScoped<IIdentityProvider, LocalProvider>();
        services.AddScoped<IProviderFactory, ProviderFactory>();
        
        services.AddExceptionHandler<IdpExceptionHandler>();
        services.AddBoosterRedisCache(redisOptions);


        return services;
    }
}
