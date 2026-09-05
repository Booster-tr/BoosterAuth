using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Idp.Providers.Local.Stores.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.ClientStores.Models;
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
    ISessionStore sessionStore,
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

        if (!client.Login.AllowedCredentialProviders.Contains(cmd.ProviderType, StringComparer.OrdinalIgnoreCase))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        var provider = providerFactory.GetCredentialProvider(cmd.ProviderType)
            ?? throw new AuthorizeException(IdpMessageKeys.ProviderNotAvailable);

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var user = await userStore.FindAsync(cmd.Identifier, client.Login.AllowedIdentifiers, ct);
        if (user is null || !user.IsActive)
            throw Invalid(IdpMessageKeys.InvalidCredentials);

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            throw Invalid(IdpMessageKeys.AccountLocked);

        var result = await provider.AuthenticateAsync(
            new CredentialRequest(cmd.Identifier, cmd.Password),
            BuildConfig(cmd.ProviderType, client), ct);

        if (!result.Succeeded)
        {
            await HandleFailedAttemptAsync(user, client.Login, ct);
            throw Invalid(IdpMessageKeys.InvalidCredentials);
        }

        await HandleSuccessfulAuthAsync(user, cmd.Password, ct);

        var (session, token) = await sessionStore.CreateAsync(
            principalId: user.Id,
            clientId: client.ClientId,
            ttl: TimeSpan.FromSeconds(client.Session.RefreshTokenTtlSeconds),
            deviceName: null,
            userAgent: cmd.UserAgent,
            ipAddress: cmd.IpAddress,
            authMethod: cmd.ProviderType,          // "local" / "ldap" — hangi yöntemle giriş
            ct: ct);

        return new CredentialLoginResult { SessionToken = token, ExpiresAt = session.ExpiresAt };
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

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            var credential = await credentialRepository.GetAsync(c => c.PrincipalId == user.Id, cancellationToken: ct);
            if (credential is not null)
            {
                credential.PasswordHash = passwordHasher.Hash(password);
                credential.PasswordChangedAt = DateTimeOffset.UtcNow;
                await credentialRepository.SaveChangesAsync(ct);
                await cacheService.RemoveByTagAsync(PrincipalStore.Tag(user.Id), ct);
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
        await cacheService.RemoveByTagAsync(PrincipalStore.Tag(principalId), ct);
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
    public required string SessionToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
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