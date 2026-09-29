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

    /// <summary>
    /// Sağlayıcının panelde doldurulacak alanları. Zorunluluklar yukarıdaki
    /// kurallardan türetilir; ekran ile sunucu doğrulaması ayrışmasın diye.
    /// </summary>
    public static IReadOnlyList<ProviderField> FieldsOf(string providerType)
    {
        if (string.Equals(providerType, Saml, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new(IdentityProviderSettings.SpEntityId, Required: true, Secret: false, Multiline: false),
                new(IdentityProviderSettings.SignOnUrl, Required: true, Secret: false, Multiline: false),
                new(IdentityProviderSettings.SigningCertificate, Required: true, Secret: false, Multiline: true)
            ];
        }

        var fields = new List<ProviderField>
        {
            new(IdentityProviderSettings.ClientId, RequiresClientId(providerType), Secret: false, Multiline: false)
        };

        if (string.Equals(providerType, Apple, StringComparison.OrdinalIgnoreCase))
        {
            fields.Add(new("team_id", Required: true, Secret: false, Multiline: false));
            fields.Add(new("key_id", Required: true, Secret: false, Multiline: false));
            // Apple'da sır, imzalama için kullanılan özel anahtardır.
            fields.Add(new(IdentityProviderSettings.ClientSecret, Required: false, Secret: true, Multiline: true));
        }
        else
        {
            fields.Add(new(IdentityProviderSettings.ClientSecret, Required: false, Secret: true, Multiline: false));
        }

        if (!HasFixedIssuer(providerType)
            && (RequiresIssuerUrl(providerType) || string.Equals(providerType, Microsoft, StringComparison.OrdinalIgnoreCase)))
        {
            fields.Add(new(IdentityProviderSettings.IssuerUrl, RequiresIssuerUrl(providerType), Secret: false, Multiline: false));
        }

        fields.Add(new("scopes", Required: false, Secret: false, Multiline: false));

        return fields;
    }
}

public record ProviderField(string Key, bool Required, bool Secret, bool Multiline);
