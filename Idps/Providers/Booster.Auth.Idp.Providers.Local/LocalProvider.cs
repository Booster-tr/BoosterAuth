using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialProviders;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Hashing.OneWays.Argon;

namespace Booster.Auth.Idp.Providers.Local;

public sealed class LocalProvider(
    ILocalUserStore userStore,
    IPasswordHasher passwordHasher)
    : ICredentialProvider
{
    public string ProviderType => "local";

    public ProviderKind Kind => ProviderKind.Credential;

    public LoginProviderType LoginProvider => LoginProviderType.Local;

    public async Task<IdentityResult> AuthenticateAsync(
        CredentialRequest request,
        ProviderConfig config,
        IReadOnlyList<IdentifierType> allowedIdentifiers,
        CancellationToken ct)
    {
        var user = await userStore.FindAsync(request.Identifier, allowedIdentifiers, ct,
            config.Settings.TryGetValue("default_country_code", out var cc) ? cc : null);

        if (user is null || !user.IsActive)
            return IdentityResult.Fail("invalid_credentials");

        if (!user.HasPassword)
            return IdentityResult.Fail("invalid_credentials");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash!))
            return IdentityResult.Fail("invalid_credentials");

        return IdentityResult.Success(
            principalId: user.Id.ToString(),
            providerKey: config.ProviderKey,
            claims: user.Claims);
    }
}