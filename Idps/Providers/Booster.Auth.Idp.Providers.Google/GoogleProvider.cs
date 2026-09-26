using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;

namespace Booster.Auth.Idp.Providers.Google;

public sealed class GoogleProvider(IHttpClientFactory httpClientFactory)
    : OidcFederationProviderBase(httpClientFactory)
{
    public const string Type = "google";

    public override string ProviderType => Type;

    protected override string FixedIssuerUrl => "https://accounts.google.com";

    protected override string DefaultScopes => "openid email profile";
}
