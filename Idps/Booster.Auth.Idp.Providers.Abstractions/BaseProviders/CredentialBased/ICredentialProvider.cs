using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialProviders;

public interface ICredentialProvider : IIdentityProvider
{
    Task<IdentityResult> AuthenticateAsync(
        CredentialRequest request,
        ProviderConfig config,
        CancellationToken ct);
}
