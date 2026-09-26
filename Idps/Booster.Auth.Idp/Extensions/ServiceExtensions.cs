using Booster.Auth.Common.Internal.Amqp.Extensions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
using Booster.Auth.Idp.Providers.Apple;
using Booster.Auth.Idp.Providers.Facebook;
using Booster.Auth.Idp.Providers.GitHub;
using Booster.Auth.Idp.Providers.Google;
using Booster.Auth.Idp.Providers.LinkedIn;
using Booster.Auth.Idp.Providers.Microsoft;
using Booster.Auth.Idp.Providers.Oidc;
using Booster.Auth.Idp.Providers.Saml;
using Booster.Auth.Idp.Providers.X;
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
using Booster.Auth.Idp.Providers.EmailCode;
using Booster.Auth.Idp.Providers.Local;
using Booster.Auth.Idp.Providers.PhoneCode;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Managers.PasswordManagers;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.FederationStores;
using Booster.Auth.Management.Grpc;
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

        services.AddBoosterMessaging<IdpDbContext>(
            configuration,
            configureRider: rider => rider.AddNotificationProducer());

        var hashingOptions = services.AddSettings<HashingOptions>(configuration);
        services.AddHashing(hashingOptions);

        services.AddScoped<OidcTokenService>();
        services.AddSingleton<OidcKeyService>();

        var managementSettings = services.AddSettings<ManagementSettings>(configuration);

        services.AddGrpcClient<ClientDataService.ClientDataServiceClient>(options =>
        {
            options.Address = new Uri(managementSettings.GrpcAddress);
        })
        .ConfigureChannel(channel =>
        {
            channel.HttpHandler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(managementSettings.TimeoutSeconds),
                EnableMultipleHttp2Connections = true
            };
        });

        services.AddScoped<IClientDataStore, ClientDataStore>();
        services.AddScoped<IPasswordPolicyManager, PasswordPolicyManager>();
        services.AddScoped<ILocalUserStore, LocalPrincipalStore>();
        services.AddScoped<ISessionStore, SessionStore>();

        services.AddScoped<IFederationProviderConfigStore, FederationProviderConfigStore>();
        services.AddScoped<ISessionCookieManager, SessionCookieManager>();

        services.AddScoped<IIdentityProvider, LocalProvider>();
        services.AddScoped<IIdentityProvider, EmailCodeProvider>();
        services.AddScoped<IIdentityProvider, PhoneCodeProvider>();
        services.AddScoped<IIdentityProvider, GoogleProvider>();
        services.AddScoped<IIdentityProvider, MicrosoftProvider>();
        services.AddScoped<IIdentityProvider, AppleProvider>();
        services.AddScoped<IIdentityProvider, LinkedInProvider>();
        services.AddScoped<IIdentityProvider, FacebookProvider>();
        services.AddScoped<IIdentityProvider, GitHubProvider>();
        services.AddScoped<IIdentityProvider, XProvider>();
        services.AddScoped<IIdentityProvider, OidcProvider>();
        services.AddScoped<IIdentityProvider, SamlProvider>();

        services.AddHttpClient(FederationProviderBase.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
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
