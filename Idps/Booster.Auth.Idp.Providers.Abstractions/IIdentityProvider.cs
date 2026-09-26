using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Idp.Providers.Abstractions;

public interface IIdentityProvider
{
    string ProviderType { get; }

    ProviderKind Kind { get; }

    LoginProviderType LoginProvider { get; }
}
