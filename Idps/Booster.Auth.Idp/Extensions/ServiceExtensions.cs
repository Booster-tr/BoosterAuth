using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Idp.Bootstrappers;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features.Token.Handlers;
using Booster.Auth.Idp.Features.Token.Handlers._base;
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
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.Hashing.Extensions;
using Booster.Hashing.Options;
using Microsoft.AspNetCore.Diagnostics;

namespace Booster.Auth.Idp.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddIdpServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettings<IdpOptions>(configuration);
        services.AddSettings<IdpUiOptions>(configuration);
        services.AddSettings<BootstrapOptions>(configuration);

        services.AddBoosterServices<IdpDbContext>(configuration);

        var hashingOptions = services.AddSettings<HashingOptions>(configuration);
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
        services.AddBoosterCacheLanguageTranslator();
        services.AddSingleton<IExceptionHandler, IdpExceptionHandler>();

        services.AddScoped<IGrantTypeHandler, AuthorizationCodeGrantHandler>();
        services.AddScoped<IGrantTypeHandler, ClientCredentialsGrantHandler>();
        services.AddScoped<IGrantTypeHandler, RefreshTokenGrantHandler>();
        services.AddScoped<IGrantTypeHandler, TokenExchangeGrantHandler>();
        services.AddScoped<TokenHandler>();
        
        return services;
    }
}
