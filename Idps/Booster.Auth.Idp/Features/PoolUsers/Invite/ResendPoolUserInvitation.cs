using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.UserManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.PoolUsers.Invite;

/// <summary>Daveti (yeniden) gonderir. Yalnizca parolasini henuz belirlememis kullanici icin; eski baglanti gecersiz olur.</summary>
public class ResendPoolUserInvitationEndpoint : Endpoint<ResendPoolUserInvitationCommand>
{
    public override void Configure()
    {
        Post("pools/users/invite");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Kullaniciya davet e-postasini yeniden gonderir");
    }

    public override async Task HandleAsync(ResendPoolUserInvitationCommand req, CancellationToken ct)
    {
        await req.ExecuteAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public record ResendPoolUserInvitationCommand(Guid UserPoolId, Guid PrincipalId) : ICommand;

public class ResendPoolUserInvitationCommandHandler(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    InvitationManager invitations,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<ResendPoolUserInvitationCommand>
{
    public async Task ExecuteAsync(ResendPoolUserInvitationCommand cmd, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(cmd.UserPoolId);

        var principal = await principalRepository.Query().AsNoTracking()
            .Include(p => p.LocalCredential)
            .FirstOrDefaultAsync(p => p.Id == cmd.PrincipalId && p.Type == PrincipalType.User, ct)
            ?? throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        if (principal.LocalCredential is not null || string.IsNullOrEmpty(principal.Email))
            throw new BusinessException(IdpMessageKeys.InvitationNotPending);

        await invitations.SendAsync(principal, cmd.UserPoolId, ct);

        // Davet olayi EF outbox'ta; SaveChanges olmadan e-posta GITMEZ (baska degisiklik olmasa da).
        await principalRepository.SaveChangesAsync(ct);
    }
}
