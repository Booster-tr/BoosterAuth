using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Managers.LoginChallengeManagers;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Hashing.OneWays.Argon;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Idp.Features.Login.CredentialLogin;



public sealed class CredentialLoginHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    ILocalUserStore userStore,
    IProviderFactory providerFactory,
    IPasswordHasher passwordHasher,
    ILoginChallengeManager loginChallengeManager,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor) : ICommandHandler<CredentialLoginCommand, CredentialLoginResult>
{
    public async Task<CredentialLoginResult> ExecuteAsync(CredentialLoginCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var provider = providerFactory.GetCredentialProvider(cmd.ProviderType)
            ?? throw new AuthorizeException(IdpMessageKeys.ProviderNotAvailable);

        if (!client.Login.AllowedLoginProviders.HasFlag(provider.LoginProvider))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var user = await userStore.FindAsync(cmd.Identifier, client.Login.AllowedIdentifiers, ct);
        if (user is null || !user.IsActive)
            throw Invalid(IdpMessageKeys.InvalidCredentials);

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            throw Invalid(IdpMessageKeys.AccountLocked);

        var result = await provider.AuthenticateAsync(
            new CredentialRequest(cmd.Identifier, cmd.Password),
            BuildConfig(cmd.ProviderType, client),
            client.Login.AllowedIdentifiers,
            ct);

        if (!result.Succeeded)
        {
            await HandleFailedAttemptAsync(user, client.Login, ct);
            throw Invalid(IdpMessageKeys.InvalidCredentials);
        }

        await HandleSuccessfulAuthAsync(user, cmd.Password, ct);

        // Parola doğru; oturum ancak havuzun istediği doğrulama/MFA adımları
        // bittiğinde açılır. Adım yoksa oturum burada açılır.
        var outcome = await loginChallengeManager.BeginAsync(
            cmd.InteractionKey, client, user,
            authMethod: cmd.ProviderType,          // "local" / "ldap" — hangi yöntemle giriş
            passwordLogin: true,
            cmd.UserAgent, cmd.IpAddress, ct);

        return new CredentialLoginResult { SessionToken = outcome.SessionToken, ExpiresAt = outcome.ExpiresAt };
    }

    private async Task HandleFailedAttemptAsync(LocalUser user, LoginPolicyData policy, CancellationToken ct)
    {
        var newCount = user.FailedAttemptCount + 1;
        DateTimeOffset? lockedUntil = newCount >= policy.MaxFailedAttempts
            ? DateTimeOffset.UtcNow.AddSeconds(policy.LockoutSeconds)
            : null;
        await UpdateLockoutAsync(user.Id, newCount, lockedUntil, ct);
    }

    private async Task HandleSuccessfulAuthAsync(LocalUser user, string password, CancellationToken ct)
    {
        if (user.FailedAttemptCount != 0 || user.LockedUntil is not null)
            await UpdateLockoutAsync(user.Id, 0, null, ct);

        if (user.HasPassword && passwordHasher.NeedsRehash(user.PasswordHash!))
        {
            var credential = await credentialRepository.GetAsync(c => c.PrincipalId == user.Id, cancellationToken: ct);
            if (credential is not null)
            {
                credential.PasswordHash = passwordHasher.Hash(password);
                credential.PasswordChangedAt = DateTimeOffset.UtcNow;
                await credentialRepository.SaveChangesAsync(ct);
                await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(user.Id), ct);
            }
        }
    }

    private async Task UpdateLockoutAsync(Guid principalId, int failedCount, DateTimeOffset? lockedUntil, CancellationToken ct)
    {
        var credential = await credentialRepository.GetAsync(c => c.PrincipalId == principalId, cancellationToken: ct);
        if (credential is null) return;

        credential.FailedAttemptCount = failedCount;
        credential.LockedUntil = lockedUntil;
        await credentialRepository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principalId), ct);
    }

    private static ProviderConfig BuildConfig(string providerType, ClientData client)
        => new() { ProviderKey = providerType, ProviderType = providerType, IsolationId = client.IsolationId, IsActive = true };

    private static ValidationException Invalid(string messageKey)
        => new([new ValidationFailure(string.Empty, messageKey)]);
}

public sealed class CredentialLoginCommand : ICommand<CredentialLoginResult>
{
    public string InteractionKey { get; set; } = default!;
    public string Identifier { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ProviderType { get; set; } = "local";
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class CredentialLoginResult
{
    /// <summary>Null ise oturum açılmadı; kullanıcı doğrulama adımına yönlendirilir.</summary>
    public string? SessionToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public bool RequiresChallenge => SessionToken is null;
}

public sealed class CredentialLoginValidator : Validator<CredentialLoginCommand>
{
    public CredentialLoginValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Identifier).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Password).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ProviderType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}