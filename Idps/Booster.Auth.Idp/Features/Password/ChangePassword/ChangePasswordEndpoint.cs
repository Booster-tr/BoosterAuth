using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.PasswordManagers;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.OneWays.Argon;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using System.Text.Json.Serialization;

namespace Booster.Auth.Idp.Features.Password.ChangePassword;

public class ChangePasswordEndpoint(
    IRequestContextAccessor requestContextAccessor,
    IClientDataStore clientDataStore,
    IPasswordPolicyManager passwordPolicyManager,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IPasswordHasher passwordHasher,
    ICacheService cacheService) : Endpoint<ChangePasswordRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("password/change");
        Summary(s =>
        {
            s.Summary = "Giriş yapmış kullanıcının parolasını değiştirir";
            s.Description = "Mevcut parola doğrulanır, havuz politikası ve parola geçmişi uygulanır. "
                          + "Bearer token zorunludur; kullanıcı token'dan çözülür.";
        });
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var context = requestContextAccessor.Context();

        var principalId = context.PrincipalId;
        if (principalId == Guid.Empty)
            throw new BusinessException(IdpMessageKeys.InvalidAccessToken);

        var client = await clientDataStore.GetByClientKeyAsync(req.ClientKey, ct)
            ?? throw new BusinessException(IdpMessageKeys.ClientNotFound);

        if (!string.Equals(client.ClientId.ToString(), context.ClientId, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException(IdpMessageKeys.InvalidClient);

        if (!string.Equals(req.NewPassword, req.NewPasswordConfirm, StringComparison.Ordinal))
            throw Invalid(nameof(ChangePasswordRequest.NewPasswordConfirm), IdpMessageKeys.PasswordsDoNotMatch);

        passwordPolicyManager.Validate(client.Password, req.NewPassword, nameof(ChangePasswordRequest.NewPassword));

        using var scope = requestContextAccessor.BeginIsolationScope(client.IsolationId);

        var credential = await credentialRepository.GetAsync(
            c => c.PrincipalId == principalId, cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        if (!passwordHasher.Verify(req.CurrentPassword, credential.PasswordHash))
            throw Invalid(nameof(ChangePasswordRequest.CurrentPassword), IdpMessageKeys.CurrentPasswordInvalid);

        await passwordPolicyManager.EnsureNotReusedAsync(
            client.Password, principalId, req.NewPassword, nameof(ChangePasswordRequest.NewPassword), ct);

        var newHash = passwordHasher.Hash(req.NewPassword);

        credential.PasswordHash = newHash;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        credential.MustChangePassword = false;
        credentialRepository.Update(credential);

        await passwordPolicyManager.RecordAsync(principalId, newHash, client.Password, ct);

        await credentialRepository.SaveChangesAsync(ct);

        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principalId), ct);

        await Send.NoContentAsync(ct);
    }

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}

public sealed class ChangePasswordRequest
{
    [JsonPropertyName("client_id")] public string ClientKey { get; init; } = default!;

    [JsonPropertyName("current_password")] public string CurrentPassword { get; init; } = default!;

    [JsonPropertyName("new_password")] public string NewPassword { get; init; } = default!;

    [JsonPropertyName("new_password_confirm")] public string NewPasswordConfirm { get; init; } = default!;
}

public sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.ClientKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^\S+$").WithMessage(IdpMessageKeys.PasswordContainsWhitespace);

        RuleFor(x => x.NewPasswordConfirm).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
