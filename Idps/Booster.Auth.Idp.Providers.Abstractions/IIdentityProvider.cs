namespace Booster.Auth.Idp.Providers.Abstractions;

public interface IIdentityProvider
{
    string ProviderType { get; }

    ProviderKind Kind { get; }
}
