using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialProviders;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationProviders;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;

namespace Booster.Auth.Idp.Providers.Abstractions;

public interface IProviderFactory
{
    ICredentialProvider? GetCredentialProvider(string providerType);

    IFederationProvider? GetFederationProvider(string providerType);

    IPasswordlessProvider? GetPasswordlessProvider(string providerType);
}

public sealed class ProviderFactory(IEnumerable<IIdentityProvider> providers) : IProviderFactory
{
    private readonly Dictionary<string, IIdentityProvider> _map =
        providers.ToDictionary(p => p.ProviderType, StringComparer.OrdinalIgnoreCase);

    public ICredentialProvider? GetCredentialProvider(string providerType)
        => Resolve<ICredentialProvider>(providerType);

    public IFederationProvider? GetFederationProvider(string providerType)
        => Resolve<IFederationProvider>(providerType);

    public IPasswordlessProvider? GetPasswordlessProvider(string providerType)
        => Resolve<IPasswordlessProvider>(providerType);

    private TProvider? Resolve<TProvider>(string providerType) where TProvider : class, IIdentityProvider
        => _map.TryGetValue(providerType, out var provider) ? provider as TProvider : null;
}
