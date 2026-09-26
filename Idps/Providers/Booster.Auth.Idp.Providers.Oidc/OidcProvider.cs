using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;

namespace Booster.Auth.Idp.Providers.Oidc;

public sealed class OidcProvider(IHttpClientFactory httpClientFactory)
    : OidcFederationProviderBase(httpClientFactory)
{
    public const string Type = "oidc";

    public override string ProviderType => Type;

    protected override string DefaultScopes => "openid profile email";
}
