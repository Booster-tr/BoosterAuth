using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;

namespace Booster.Auth.Idp.Providers.Microsoft;

public sealed class MicrosoftProvider(IHttpClientFactory httpClientFactory)
    : OidcFederationProviderBase(httpClientFactory)
{
    public const string Type = "microsoft";

    public override string ProviderType => Type;

    protected override string? FixedIssuerUrl => null;

    protected override string DefaultScopes => "openid email profile";

    protected override string? FallbackIssuerUrl => "https://login.microsoftonline.com/common/v2.0";
}
