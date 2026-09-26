using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;

namespace Booster.Auth.Idp.Providers.EmailCode;

public sealed class EmailCodeProvider(
    ILocalUserStore principalStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher)
    : PasswordlessProviderBase(principalStore, codeStore, codeHasher)
{

    public const string Type = "email_code";

    public override string ProviderType => Type;

    public override IdentifierType IdentifierType => IdentifierType.Email;

    public override LoginProviderType LoginProvider => LoginProviderType.EmailCode;

    protected override string Normalize(string identifier, ProviderConfig config)
        => identifier.Trim().ToLowerInvariant();

    protected override string? ResolveTarget(LocalUser principal)
        => principal.Email;
}
