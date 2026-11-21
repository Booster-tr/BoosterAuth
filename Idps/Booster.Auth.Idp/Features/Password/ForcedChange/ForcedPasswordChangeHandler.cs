using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Managers.LoginChallengeManagers;
using Booster.Auth.Idp.Managers.PasswordManagers;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Hashing.OneWays.Argon;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Idp.Features.Password.ForcedChange;

/// <summary>
/// Giriste zorunlu parola degisimi (LocalCredential.MustChangePassword).
/// <para>
/// Akis: CredentialLoginHandler parolayi dogrular; degisim zorunluysa oturum ACMAZ,
/// bu kaydi etkilesim anahtarina bagli olarak kisa sureligine saklar ve kullaniciyi
/// /ui/change-password ekranina yollar. Mevcut parola az once dogrulandigi icin
/// tekrar sorulmaz. Yeni parola kaydedilince giris normal sekilde (dogrulama / MFA
/// adimlari dahil) devam eder.
/// </para>
/// </summary>
public sealed record ForcedPasswordChangeData
{
    public required Guid PrincipalId { get; init; }
    public required Guid IsolationId { get; init; }
    public required string ClientKey { get; init; }
    public required string AuthMethod { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Parola dogrulandiktan sonra yeni parolanin belirlenmesi icin taninan sure.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
}

public sealed class ForcedPasswordChangeHandler(
    ISecureCacheStore<ForcedPasswordChangeData> pendingStore,
    IClientDataStore clientDataStore,
    ILocalUserStore userStore,
    IPasswordPolicyManager passwordPolicyManager,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IPasswordHasher passwordHasher,
    ILoginChallengeManager loginChallengeManager,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor) : ICommandHandler<ForcedPasswordChangeCommand, LoginOutcome>
{
    public async Task<LoginOutcome> ExecuteAsync(ForcedPasswordChangeCommand cmd, CancellationToken ct)
    {
        var pending = await pendingStore.GetAsync(cmd.InteractionKey, ct);
        if (pending is null || pending.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        if (!string.Equals(cmd.Password, cmd.PasswordConfirm, StringComparison.Ordinal))
            throw Invalid(nameof(ForcedPasswordChangeCommand.PasswordConfirm), IdpMessageKeys.PasswordsDoNotMatch);

        var client = await clientDataStore.GetByClientKeyAsync(pending.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        passwordPolicyManager.Validate(client.Password, cmd.Password, nameof(ForcedPasswordChangeCommand.Password));

        using var scope = requestContextAccessor.BeginSystemContext(pending.IsolationId);

        // Gecmis parolalar (mevcut parola dahil) havuz politikasina gore tekrar kullanilamaz.
        await passwordPolicyManager.EnsureNotReusedAsync(
            client.Password, pending.PrincipalId, cmd.Password, nameof(ForcedPasswordChangeCommand.Password), ct);

        var credential = await credentialRepository.GetAsync(c => c.PrincipalId == pending.PrincipalId, cancellationToken: ct)
            ?? throw new AuthorizeException(IdpMessageKeys.PrincipalNotFound);

        var newHash = passwordHasher.Hash(cmd.Password);
        credential.PasswordHash = newHash;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        credential.MustChangePassword = false;
        credentialRepository.Update(credential);

        await passwordPolicyManager.RecordAsync(pending.PrincipalId, newHash, client.Password, ct);
        await credentialRepository.SaveChangesAsync(ct);

        await pendingStore.RemoveAsync(cmd.InteractionKey, ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(pending.PrincipalId), ct);

        var user = await userStore.GetByIdAsync(pending.PrincipalId, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.PrincipalNotFound);

        // Parola degisti; giris kaldigi yerden devam eder (MFA / dogrulama adimlari dahil).
        return await loginChallengeManager.BeginAsync(
            cmd.InteractionKey, client, user,
            authMethod: pending.AuthMethod,
            passwordLogin: true,
            cmd.UserAgent, cmd.IpAddress, ct);
    }

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}

public sealed class ForcedPasswordChangeCommand : ICommand<LoginOutcome>
{
    public string InteractionKey { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string PasswordConfirm { get; set; } = default!;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class ForcedPasswordChangeValidator : Validator<ForcedPasswordChangeCommand>
{
    public ForcedPasswordChangeValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^\S+$").WithMessage(IdpMessageKeys.PasswordContainsWhitespace);

        RuleFor(x => x.PasswordConfirm).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
