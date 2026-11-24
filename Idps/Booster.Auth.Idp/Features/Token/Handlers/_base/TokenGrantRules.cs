using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Idp.Features.Token.Handlers._base;

/// <summary>
/// Kod ve yenileme akislarinin ortak kurallari: erisim token'inin kapsami ve audience'i.
/// </summary>
public static class TokenGrantRules
{
    /// <summary>
    /// Kapsam = istenen standart OIDC kapsamlari (openid, profile, email, offline_access) + kullaniciya dogrudan verilen
    /// kapsamlar + kullanicinin ROLLERININ kapsamlari (rol -> kapsam eslemesi Management'tan uygulama verisiyle gelir).
    /// Ozel bir kapsami istemek yetmez, kullanicida ya da rolunde olmalidir.
    /// </summary>
    public static string BuildScope(string requestedScope, IEnumerable<string> principalScopes,
        IEnumerable<Guid> roleIds, ClientData client)
    {
        var roles = roleIds.ToHashSet();
        var fromRoles = client.RoleScopes.Where(r => roles.Contains(r.RoleId)).SelectMany(r => r.Scopes);

        return string.Join(' ', requestedScope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(OidcScopes.IsStandard)
            .Union(principalScopes, StringComparer.Ordinal)
            .Union(fromRoles, StringComparer.Ordinal));
    }

    /// <summary>
    /// Audience: token isteginde verilen <c>resource</c>, yoksa authorize'da verilen (koda bagli), o da yoksa IdP'nin
    /// kendisi (issuer). Boylece <c>resource</c> vermeyen uygulamanin token'i da <c>/idp/me</c>'de calisir. Issuer disindaki
    /// her audience uygulamanin izinli API'lerinde (AllowedAudiences) olmalidir.
    /// </summary>
    public static string ResolveAudience(string? requested, string? bound, ClientData client, string issuer)
    {
        var self = issuer.TrimEnd('/');
        var audience = !string.IsNullOrWhiteSpace(requested) ? requested.Trim()
            : !string.IsNullOrWhiteSpace(bound) ? bound.Trim()
            : self;

        if (!IsAllowed(audience, client, self))
            throw new BusinessException(IdpMessageKeys.AudienceNotAllowed);

        return audience;
    }

    public static bool IsAllowed(string audience, ClientData client, string issuer)
        => string.Equals(audience.TrimEnd('/'), issuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
           || client.AllowedAudiences.Any(a => string.Equals(a.Trim().TrimEnd('/'), audience.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
}
