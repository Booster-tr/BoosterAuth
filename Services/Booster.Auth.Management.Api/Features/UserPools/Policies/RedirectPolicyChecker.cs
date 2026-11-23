using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Licensing.Domains;

namespace Booster.Auth.Management.Api.Features.UserPools.Policies;

/// <summary>
/// Havuzun redirect politikasina uymayan adresleri bulur. Client olusturma/guncellemede reddetmek ve
/// mevcut client'larda (politika sonradan acildiysa) uyari gostermek icin AYNI kural kullanilir.
/// Domain eslesmesi lisans domain kurali ile aynidir: domainin kendisi veya alt alani.
/// </summary>
public static class RedirectPolicyChecker
{
    public static IReadOnlyList<string> Violations(RedirectPolicy? policy, IEnumerable<string> uris)
    {
        if (policy is not { Enabled: true })
            return [];

        return uris.Where(u => !IsAllowed(policy, u)).Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool IsAllowed(RedirectPolicy policy, string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;

        var web = parsed.Scheme is "http" or "https";
        if (!web)
            return policy.AllowCustomSchemes;

        if (DomainRules.IsTestHost(parsed.Host))
            return policy.AllowTestHosts;

        return DomainRules.Check(parsed.Host, policy.AllowedDomains) == DomainCheck.Allowed;
    }
}
