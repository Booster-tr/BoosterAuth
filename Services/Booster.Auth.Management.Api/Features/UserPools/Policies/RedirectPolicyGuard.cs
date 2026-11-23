using Booster.Auth.Management.Api.Features._base.Entities;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Management.Api.Features.UserPools.Policies;

/// <summary>Client olusturma/guncellemede: politikaya uymayan adres varsa dogrulama hatasi (hangi adresler oldugu mesajda).</summary>
public static class RedirectPolicyGuard
{
    public static void EnsureAllowed(RedirectPolicy? policy, IEnumerable<string> uris)
    {
        var bad = RedirectPolicyChecker.Violations(policy, uris);
        if (bad.Count == 0) return;

        throw new ValidationException([
            new ValidationFailure("RedirectUris", AuthNMessageKeys.RedirectDomainNotAllowed)
            {
                FormattedMessagePlaceholderValues = new Dictionary<string, object> { ["Uris"] = string.Join(", ", bad) }
            }
        ]);
    }
}
