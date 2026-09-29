using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.UserPools.UpdateDefaultRoles;

public class UpdateUserPoolDefaultRolesEndpoint : Endpoint<UpdateUserPoolDefaultRolesCommand, UpdateUserPoolDefaultRolesResponse>
{
    public override void Configure()
    {
        Put("default-roles");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Havuzun varsayılan rol listesini günceller";
            s.Description = "Bu havuza kayıt olan kullanıcılara otomatik atanacak rolleri belirler. " +
                            "Liste tamamen değiştirilir; her rol bu havuza ait olmalıdır.";
        });
    }

    public override async Task HandleAsync(UpdateUserPoolDefaultRolesCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateUserPoolDefaultRolesCommand(Guid UserPoolId, List<Guid> RoleIds) : ICommand<UpdateUserPoolDefaultRolesResponse>;

public readonly record struct UpdateUserPoolDefaultRolesResponse(Guid Id);

public class UpdateUserPoolDefaultRolesCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateUserPoolDefaultRolesCommand, UpdateUserPoolDefaultRolesResponse>
{
    public async Task<UpdateUserPoolDefaultRolesResponse> ExecuteAsync(
        UpdateUserPoolDefaultRolesCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var distinctRoleIds = command.RoleIds.Distinct().ToList();

        if (distinctRoleIds.Count > 0)
        {
            var poolRoleCount = await roleRepository.CountAsync(
                r => r.UserPoolId == command.UserPoolId && distinctRoleIds.Contains(r.Id),
                cancellationToken: ct);

            if (poolRoleCount != distinctRoleIds.Count)
                throw new BusinessException(AuthNMessageKeys.RoleNotFound);
        }

        pool!.DefaultRoleIds = distinctRoleIds;

        await userPoolRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UpdateUserPoolDefaultRolesResponse(pool.Id);
    }
}

public class UpdateUserPoolDefaultRolesValidator : Validator<UpdateUserPoolDefaultRolesCommand>
{
    public UpdateUserPoolDefaultRolesValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.RoleIds).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
