using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;

namespace Booster.Auth.Idp.Providers.LinkedIn;

public sealed class LinkedInProvider(IHttpClientFactory httpClientFactory)
    : OidcFederationProviderBase(httpClientFactory)
{
    public const string Type = "linkedin";

    public override string ProviderType => Type;

    protected override string? FixedIssuerUrl => "https://www.linkedin.com/oauth";

    protected override string DefaultScopes => "openid profile email";
    protected override bool UsePkce => false;
}
