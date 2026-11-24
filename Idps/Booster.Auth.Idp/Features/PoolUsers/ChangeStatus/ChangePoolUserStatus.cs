using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Managers.AccountManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.PoolUsers.ChangeStatus;

/// <summary>
/// Havuz kullanicisini askiya alir ya da yeniden etkinlestirir (<c>Active</c> / <c>Suspended</c>). Silmeden farkli olarak
/// kayit, roller ve yetki iliskileri korunur. Yonetim havuzunda kisi kendini ve son yoneticiyi askiya alamaz.
/// </summary>
public class ChangePoolUserStatusEndpoint : Endpoint<ChangePoolUserStatusCommand, ChangePoolUserStatusResponse>
{
    public override void Configure()
    {
        Put("pools/users/status");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuz kullanıcısını askıya alır ya da etkinleştirir";
            s.Description = "Askıya alınan kullanıcı giriş yapamaz ve token yenileyemez; oturumları hemen kapatılır.";
        });
    }

    // Havuz ve kullanici QUERY'den (FGA ayni degeri kontrol eder); govdede yalnizca durum.
    public override async Task HandleAsync(ChangePoolUserStatusCommand req, CancellationToken ct)
        => await Send.OkAsync(await (req with
        {
            UserPoolId = Query<Guid>("UserPoolId"),
            PrincipalId = Query<Guid>("PrincipalId")
        }).ExecuteAsync(ct), ct);
}

public record ChangePoolUserStatusCommand(Guid UserPoolId, Guid PrincipalId, PrincipalStatus Status)
    : ICommand<ChangePoolUserStatusResponse>;

public readonly record struct ChangePoolUserStatusResponse(Guid PrincipalId, PrincipalStatus Status);

public class ChangePoolUserStatusCommandHandler(
    IdpDbContext db,
    AccountService accountService,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<ChangePoolUserStatusCommand, ChangePoolUserStatusResponse>
{
    public async Task<ChangePoolUserStatusResponse> ExecuteAsync(ChangePoolUserStatusCommand command, CancellationToken ct)
    {
        var management = command.UserPoolId == AuthConstants.ManagementPoolId;
        if (management && command.Status != PrincipalStatus.Active && command.PrincipalId == requestContextAccessor.Context().PrincipalId)
            throw new ForbiddenException(IdpMessageKeys.CannotSuspendSelf);

        using (requestContextAccessor.BeginSystemContext(command.UserPoolId))
        {
            var exists = await db.Principals.AnyAsync(p => p.Id == command.PrincipalId && p.Type == PrincipalType.User, ct);
            if (!exists)
                throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

            if (management && command.Status != PrincipalStatus.Active)
            {
                var isAdmin = await db.PrincipalRoles.AnyAsync(r => r.PrincipalId == command.PrincipalId && r.RoleName == AuthConstants.AdminRoleName, ct);
                var otherAdmins = await db.PrincipalRoles
                    .Where(r => r.PrincipalId != command.PrincipalId && r.RoleName == AuthConstants.AdminRoleName)
                    .Join(db.Principals.Where(p => p.Status == PrincipalStatus.Active), r => r.PrincipalId, p => p.Id, (r, _) => r)
                    .CountAsync(ct);
                if (isAdmin && otherAdmins == 0)
                    throw new ForbiddenException(IdpMessageKeys.CannotSuspendLastAdmin);
            }
        }

        await accountService.SetStatusAsync(command.PrincipalId, command.UserPoolId, command.Status, ct);
        return new ChangePoolUserStatusResponse(command.PrincipalId, command.Status);
    }
}

public class ChangePoolUserStatusValidator : Validator<ChangePoolUserStatusCommand>
{
    public ChangePoolUserStatusValidator()
    {
        // Yalnizca iki durum: "Deactivated" panelde kullanilmaz.
        RuleFor(x => x.Status).Must(s => s is PrincipalStatus.Active or PrincipalStatus.Suspended)
            .WithMessage(IdpMessageKeys.PrincipalStatusInvalid);
    }
}
