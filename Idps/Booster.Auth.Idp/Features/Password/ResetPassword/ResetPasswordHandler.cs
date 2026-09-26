using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Managers.PasswordManagers;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.PasswordResetStores;
using Booster.Auth.Idp.Stores.PasswordResetStores.Models;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Hashing.OneWays.Argon;
using Booster.Hashing.OneWays.Simple;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Password.ResetPassword;

public sealed class ResetPasswordHandler(
    ISecureCacheStore<PasswordResetData> resetStore,
    IClientDataStore clientDataStore,
    IPasswordPolicyManager passwordPolicyManager,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    IAsyncRepository<RefreshToken, IdpDbContext> refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ISimpleHasher simpleHasher,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor) : ICommandHandler<ResetPasswordCommand, ResetPasswordResult>
{
    public async Task<ResetPasswordResult> ExecuteAsync(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var reset = await resetStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ResetTokenInvalid);

        if (reset.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await ConsumeAsync(cmd.InteractionKey, reset, ct);
            throw Invalid(nameof(ResetPasswordCommand.Code), IdpMessageKeys.ResetTokenInvalid);
        }

        if (!simpleHasher.Verify(cmd.Code.Trim(), reset.CodeHash))
        {
            await RegisterFailedAttemptAsync(cmd.InteractionKey, reset, ct);
            throw Invalid(nameof(ResetPasswordCommand.Code), IdpMessageKeys.ResetTokenInvalid);
        }

        if (!string.Equals(cmd.Password, cmd.PasswordConfirm, StringComparison.Ordinal))
            throw Invalid(nameof(ResetPasswordCommand.PasswordConfirm), IdpMessageKeys.PasswordsDoNotMatch);

        var client = await clientDataStore.GetByClientKeyAsync(reset.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        passwordPolicyManager.Validate(client.Password, cmd.Password, nameof(ResetPasswordCommand.Password));

        using var scope = requestContextAccessor.BeginSystemContext(reset.IsolationId);

        await passwordPolicyManager.EnsureNotReusedAsync(client.Password, reset.PrincipalId, cmd.Password, nameof(ResetPasswordCommand.Password), ct);

        var credential = await credentialRepository.GetAsync(
            c => c.PrincipalId == reset.PrincipalId, cancellationToken: ct)
            ?? throw new AuthorizeException(IdpMessageKeys.PrincipalNotFound);

        var newHash = passwordHasher.Hash(cmd.Password);

        credential.PasswordHash = newHash;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        credential.MustChangePassword = false;

        credential.FailedAttemptCount = 0;
        credential.LockedUntil = null;

        credentialRepository.Update(credential);

        await passwordPolicyManager.RecordAsync(reset.PrincipalId, newHash, client.Password, ct);

        await RevokeAllSessionsAsync(reset.PrincipalId, ct);

        await credentialRepository.SaveChangesAsync(ct);

        await ConsumeAsync(cmd.InteractionKey, reset, ct);

        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(reset.PrincipalId), ct);

        return new ResetPasswordResult { InteractionKey = cmd.InteractionKey };
    }

    private async Task RevokeAllSessionsAsync(Guid principalId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var sessions = await sessionRepository.Query()
            .Where(s => s.PrincipalId == principalId && s.Status == SessionStatus.Active)
            .ToListAsync(ct);

        if (sessions.Count == 0)
            return;

        var sessionIds = sessions.Select(s => s.Id).ToList();

        foreach (var session in sessions)
        {
            session.Status = SessionStatus.Revoked;
            session.RevokedAt = now;
            sessionRepository.Update(session);
        }

        var tokens = await refreshTokenRepository.Query()
            .Where(r => sessionIds.Contains(r.SessionId) && r.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            refreshTokenRepository.Update(token);
        }

        foreach (var id in sessionIds)
            await cacheService.RemoveByTagAsync(SessionStore.Tag(id), ct);
    }

    private async Task RegisterFailedAttemptAsync(
        string interactionKey, PasswordResetData reset, CancellationToken ct)
    {
        const int maxAttempts = 5;

        if (reset.AttemptCount + 1 >= maxAttempts)
        {
            await ConsumeAsync(interactionKey, reset, ct);
            return;
        }

        await resetStore.StoreAsync(
            PasswordResetKeys.ForInteraction(interactionKey),
            reset with { AttemptCount = reset.AttemptCount + 1 },
            ResetCodeTtl,
            ct);
    }

    private async Task ConsumeAsync(string interactionKey, PasswordResetData reset, CancellationToken ct)
    {
        await resetStore.RemoveAsync(PasswordResetKeys.ForInteraction(interactionKey), ct);
        await resetStore.RemoveAsync(PasswordResetKeys.Pending(reset.IsolationId, reset.PrincipalId), ct);
    }

    private static readonly TimeSpan ResetCodeTtl = TimeSpan.FromMinutes(15);

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}

public sealed class ResetPasswordCommand : ICommand<ResetPasswordResult>
{
    public string InteractionKey { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string PasswordConfirm { get; set; } = default!;
}

public sealed class ResetPasswordResult
{
    public string? InteractionKey { get; init; }
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^[0-9]{6}$").WithMessage(IdpMessageKeys.ResetTokenInvalid);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^\S+$").WithMessage(IdpMessageKeys.PasswordContainsWhitespace);

        RuleFor(x => x.PasswordConfirm).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
