using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Management.Api.Features.IdentityProviders._base;

public static class IdentityProviderSettings
{
    public const string ClientId = "client_id";
    public const string ClientSecret = "client_secret";
    public const string IssuerUrl = "issuer_url";
    public const string SpEntityId = "sp_entity_id";
    public const string SignOnUrl = "sign_on_url";
    public const string SigningCertificate = "signing_certificate";

    public static Dictionary<string, string> Normalize(
        Dictionary<string, string>? settings, string providerType)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, value) in settings ?? [])
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                continue;

            var key = name.Trim().ToLowerInvariant();

            if (key == ClientSecret)
                continue;

            normalized[key] = value.Trim();
        }

        if (FederationProviderCatalog.HasFixedIssuer(providerType))
            normalized.Remove(IssuerUrl);

        return normalized;
    }

    public static void EnsureRequired(string providerType, IReadOnlyDictionary<string, string> settings)
    {
        if (FederationProviderCatalog.RequiresClientId(providerType) && !Has(settings, ClientId))
            throw new BusinessException(AuthNMessageKeys.IdentityProviderClientIdRequired);

        if (FederationProviderCatalog.RequiresIssuerUrl(providerType) && !Has(settings, IssuerUrl))
            throw new BusinessException(AuthNMessageKeys.IdentityProviderIssuerRequired);

        if (!string.Equals(providerType, FederationProviderCatalog.Saml, StringComparison.OrdinalIgnoreCase))
            return;

        if (!Has(settings, SpEntityId) || !Has(settings, SignOnUrl) || !Has(settings, SigningCertificate))
            throw new BusinessException(AuthNMessageKeys.IdentityProviderSamlSettingsRequired);
    }

    private static bool Has(IReadOnlyDictionary<string, string> settings, string name)
        => settings.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);
}
