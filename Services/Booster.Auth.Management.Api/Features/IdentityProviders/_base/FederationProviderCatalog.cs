using L = Booster.Auth.Ldap.LdapSettings;

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
    /// <summary>LDAP / Active Directory: giris ekraninda buton DEGIL; parola formu dizinle dogrular.</summary>
    public const string Ldap = Booster.Auth.Ldap.LdapSettings.ProviderType;

    private static readonly HashSet<string> FixedIssuerTypes =
        new(StringComparer.OrdinalIgnoreCase) { Google, Apple, LinkedIn };

    private static readonly HashSet<string> NoClientIdTypes =
        new(StringComparer.OrdinalIgnoreCase) { Saml, Ldap };

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Google, Microsoft, Apple, LinkedIn, Facebook, GitHub, X, Oidc, Saml, Ldap
        };

    public static bool IsLdap(string? providerType)
        => string.Equals(providerType, Ldap, StringComparison.OrdinalIgnoreCase);

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
        if (IsLdap(providerType))
        {
            // Varsayilanlar Active Directory'ye gore; OpenLDAP icin filtre "(uid={0})", kimlik alani "entryUUID".
            return
            [
                new(L.KeyHost, Required: true, Secret: false, Multiline: false),
                new(L.KeySecurity, Required: true, Secret: false, Multiline: false, Options: L.SecurityOptions, Default: L.SecurityLdaps),
                new(L.KeyPort, Required: true, Secret: false, Multiline: false, Default: "636"),
                new(L.KeyBindDn, Required: true, Secret: false, Multiline: false),
                new(IdentityProviderSettings.ClientSecret, Required: true, Secret: true, Multiline: false),
                new(L.KeyBaseDn, Required: true, Secret: false, Multiline: false),
                new(L.KeyUserFilter, Required: true, Secret: false, Multiline: false, Default: L.DefaultUserFilter),
                new(L.KeyUsernameAttribute, Required: false, Secret: false, Multiline: false, Default: "sAMAccountName"),
                new(L.KeyEmailAttribute, Required: false, Secret: false, Multiline: false, Default: "mail"),
                new(L.KeyNameAttribute, Required: false, Secret: false, Multiline: false, Default: "displayName"),
                new(L.KeyIdAttribute, Required: false, Secret: false, Multiline: false, Default: "objectGUID"),
            ];
        }

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

/// <param name="Options">Doluysa panel secim listesi gosterir.</param>
/// <param name="Default">Yeni saglayicida alanin on degeri.</param>
public record ProviderField(string Key, bool Required, bool Secret, bool Multiline, IReadOnlyList<string>? Options = null, string? Default = null);
