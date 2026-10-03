using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Roles.Delete;

public class DeleteRoleEndpoint : EndpointWithoutRequest<DeleteRoleResponse>
{
    public override void Configure()
    {
        Delete("");
        Group<RolesEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Rolü siler";
            s.Description = "Rolü soft-delete ile kaldırır ve " +
                            "havuzun varsayılan rollerinden çıkarılır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteRoleCommand(Query<Guid>("UserPoolId"), Query<Guid>("RoleId")).ExecuteAsync(ct), ct);
}

public record DeleteRoleCommand(Guid UserPoolId, Guid RoleId) : ICommand<DeleteRoleResponse>;

public readonly record struct DeleteRoleResponse(Guid Id);

public class DeleteRoleCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<DeleteRoleCommand, DeleteRoleResponse>
{
    public async Task<DeleteRoleResponse> ExecuteAsync(DeleteRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetAsync(
            r => r.Id == command.RoleId && r.UserPoolId == command.UserPoolId, cancellationToken: ct);
        role.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleNotFound);

        roleRepository.Delete(role!);

        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);

        if (pool is not null && pool.DefaultRoleIds.Contains(role!.Id))
            pool.DefaultRoleIds = pool.DefaultRoleIds.Where(id => id != role.Id).ToList();

        await roleRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(command.UserPoolId, ct);

        return new DeleteRoleResponse(role!.Id);
    }
}
