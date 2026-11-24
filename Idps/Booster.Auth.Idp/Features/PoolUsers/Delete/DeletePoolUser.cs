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

namespace Booster.Auth.Idp.Features.PoolUsers.Delete;

/// <summary>
/// Panelden havuz kullanicisi silme. Lisans limiti asildiginda panel salt okunurken de calisir (DELETE serbest),
/// boylece musteri kullanici silerek limitin altina inebilir.
/// </summary>
public class DeletePoolUserEndpoint : EndpointWithoutRequest<DeletePoolUserResponse>
{
    public override void Configure()
    {
        Delete("pools/users");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuz kullanıcısını siler";
            s.Description = "Oturumlar kapatılır; parola, dış kimlikler, izinler, roller ve yetki ilişkileri silinir; kayıt " +
                            "anonimleştirilip silinmiş işaretlenir. Lisans kullanıcı sayısından düşer.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeletePoolUserCommand(Query<Guid>("UserPoolId"), Query<Guid>("PrincipalId")).ExecuteAsync(ct), ct);
}

public record DeletePoolUserCommand(Guid UserPoolId, Guid PrincipalId) : ICommand<DeletePoolUserResponse>;

public readonly record struct DeletePoolUserResponse(Guid PrincipalId);

public class DeletePoolUserCommandHandler(
    IdpDbContext db,
    AccountService accountService,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<DeletePoolUserCommand, DeletePoolUserResponse>
{
    public async Task<DeletePoolUserResponse> ExecuteAsync(DeletePoolUserCommand command, CancellationToken ct)
    {
        // Panel kullanicilari (yonetim havuzu) da silinebilir: ayrilan bir yonetici sistemden cikarilabilmeli. Iki
        // istisna: kisi kendini silemez (kendini kilitlemesin) ve son yonetici silinemez (panel sahipsiz kalmasin).
        if (command.UserPoolId == AuthConstants.ManagementPoolId && command.PrincipalId == requestContextAccessor.Context().PrincipalId)
            throw new ForbiddenException(IdpMessageKeys.CannotDeleteSelf);

        using (requestContextAccessor.BeginSystemContext(command.UserPoolId))
        {
            var exists = await db.Principals.AnyAsync(p => p.Id == command.PrincipalId && p.Type == PrincipalType.User, ct);
            if (!exists)
                throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

            if (command.UserPoolId == AuthConstants.ManagementPoolId)
            {
                var isAdmin = await db.PrincipalRoles.AnyAsync(r => r.PrincipalId == command.PrincipalId && r.RoleName == AuthConstants.AdminRoleName, ct);
                var otherAdmins = await db.PrincipalRoles
                    .Where(r => r.PrincipalId != command.PrincipalId && r.RoleName == AuthConstants.AdminRoleName)
                    .Join(db.Principals.Where(p => p.Status == PrincipalStatus.Active), r => r.PrincipalId, p => p.Id, (r, _) => r)
                    .CountAsync(ct);
                if (isAdmin && otherAdmins == 0)
                    throw new ForbiddenException(IdpMessageKeys.CannotDeleteLastAdmin);
            }
        }

        await accountService.DeletePrincipalAsync(command.PrincipalId, command.UserPoolId, ct);

        return new DeletePoolUserResponse(command.PrincipalId);
    }
}

public class DeletePoolUserValidator : Validator<DeletePoolUserCommand>
{
    public DeletePoolUserValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
