using Booster.Auth.Common.Internal.Auditing;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Managers.PasswordManagers;
using Booster.Auth.Idp.Managers.UserManagers;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.Hashing.OneWays.Argon;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Invitations;

public sealed class AcceptInvitationCommand : ICommand
{
    public string Token { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string PasswordConfirm { get; set; } = default!;
}

/// <summary>
/// Davet baglantisiyla parola belirleme. Baglantiya tiklanmasi e-postanin kullaniciya ait oldugunu kanitlar;
/// bu yuzden e-posta DOGRULANMIS sayilir. Baglanti tek kullanimliktir.
/// </summary>
public sealed class AcceptInvitationHandler(
    InvitationManager invitations,
    PoolDataReader poolDataReader,
    IPasswordPolicyManager passwordPolicyManager,
    IPasswordHasher passwordHasher,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor,
    AuditLog audit) : ICommandHandler<AcceptInvitationCommand>
{
    public async Task ExecuteAsync(AcceptInvitationCommand cmd, CancellationToken ct)
    {
        var invitation = await invitations.ResolveAsync(cmd.Token, ct)
                         ?? throw new AuthorizeException(IdpMessageKeys.InvitationInvalid);

        if (!string.Equals(cmd.Password, cmd.PasswordConfirm, StringComparison.Ordinal))
            throw Invalid(nameof(cmd.PasswordConfirm), IdpMessageKeys.PasswordsDoNotMatch);

        var pool = await poolDataReader.GetAsync(invitation.IsolationId, ct)
                   ?? throw new AuthorizeException(IdpMessageKeys.InvitationInvalid);

        passwordPolicyManager.Validate(pool.Password, cmd.Password, nameof(cmd.Password));

        using var _ = requestContextAccessor.BeginSystemContext(invitation.IsolationId);

        var principal = await principalRepository.Query()
            .Include(p => p.LocalCredential)
            .FirstOrDefaultAsync(p => p.Id == invitation.PrincipalId, ct);

        // Kullanici silinmis ya da parolasini zaten belirlemis: baglanti artik bir ise yaramaz.
        if (principal is null || principal.LocalCredential is not null)
        {
            await invitations.ConsumeAsync(cmd.Token, invitation, ct);
            throw new AuthorizeException(IdpMessageKeys.InvitationInvalid);
        }

        var now = DateTimeOffset.UtcNow;
        var hash = passwordHasher.Hash(cmd.Password);

        await credentialRepository.AddAsync(new LocalCredential
        {
            Id = EntityId.New(),
            PrincipalId = principal.Id,
            PasswordHash = hash,
            PasswordChangedAt = now,
            CreatedTime = now
        }, ct);

        principal.EmailVerified = true;
        principalRepository.Update(principal);

        await passwordPolicyManager.RecordAsync(principal.Id, hash, pool.Password, ct);
        await principalRepository.SaveChangesAsync(ct);

        await invitations.ConsumeAsync(cmd.Token, invitation, ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principal.Id), ct);
        audit.Success("account.invitation_accepted", new AuditEntry(PrincipalId: principal.Id, PoolId: invitation.IsolationId));
    }

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}
