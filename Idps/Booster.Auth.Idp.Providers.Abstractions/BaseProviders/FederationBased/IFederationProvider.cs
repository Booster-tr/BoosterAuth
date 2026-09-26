using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationProviders;

public interface IFederationProvider : IIdentityProvider
{
    Task<FederationChallenge> BuildChallengeAsync(
        FederationContext context,
        ProviderConfig config,
        CancellationToken ct);

    Task<FederationResult> HandleCallbackAsync(
        FederationCallback callback,
        ProviderConfig config,
        CancellationToken ct);
}
