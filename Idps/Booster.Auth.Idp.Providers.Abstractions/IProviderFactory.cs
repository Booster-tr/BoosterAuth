using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialProviders;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationProviders;

namespace Booster.Auth.Idp.Providers.Abstractions;

public interface IProviderFactory
{
    ICredentialProvider? GetCredentialProvider(string providerType);

    IFederationProvider? GetFederationProvider(string providerType);
}

public sealed class ProviderFactory(IEnumerable<IIdentityProvider> providers) : IProviderFactory
{
    private readonly Dictionary<string, IIdentityProvider> _map =
        providers.ToDictionary(p => p.ProviderType, StringComparer.OrdinalIgnoreCase);

    public ICredentialProvider? GetCredentialProvider(string providerType)
        => _map.TryGetValue(providerType, out var p) && p is ICredentialProvider cp ? cp : null;

    public IFederationProvider? GetFederationProvider(string providerType)
        => _map.TryGetValue(providerType, out var p) && p is IFederationProvider fp ? fp : null;
}