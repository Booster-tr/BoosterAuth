namespace Booster.Auth.Management.Api.Features.IdentityProviders._base;

public static class FederationProviderCatalog
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
    public const string Apple = "apple";
    public const string LinkedIn = "linkedin";
    public const string Facebook = "facebook";
    public const string GitHub = "github";
    public const string X = "x";
    public const string Oidc = "oidc";
    public const string Saml = "saml";

    private static readonly HashSet<string> FixedIssuerTypes =
        new(StringComparer.OrdinalIgnoreCase) { Google, Apple, LinkedIn };

    private static readonly HashSet<string> NoClientIdTypes =
        new(StringComparer.OrdinalIgnoreCase) { Saml };

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Google, Microsoft, Apple, LinkedIn, Facebook, GitHub, X, Oidc, Saml
        };

    public static bool IsKnown(string? providerType)
        => providerType is not null && All.Contains(providerType);

    public static bool RequiresClientId(string providerType)
        => !NoClientIdTypes.Contains(providerType);

    public static bool RequiresIssuerUrl(string providerType)
        => string.Equals(providerType, Oidc, StringComparison.OrdinalIgnoreCase);

    public static bool HasFixedIssuer(string providerType)
        => FixedIssuerTypes.Contains(providerType);
}
