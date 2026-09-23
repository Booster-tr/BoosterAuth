using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Roles.Delete;

public class DeleteRoleEndpoint : EndpointWithoutRequest<DeleteRoleResponse>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<RolesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Rolü siler";
            s.Description = "Rolü soft-delete ile kaldırır. Role bağlı scope kayıtları cascade ile düşer.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteRoleCommand(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record DeleteRoleCommand(Guid Id) : ICommand<DeleteRoleResponse>;

public readonly record struct DeleteRoleResponse(Guid Id);

public class DeleteRoleCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<DeleteRoleCommand, DeleteRoleResponse>
{
    public async Task<DeleteRoleResponse> ExecuteAsync(DeleteRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetAsync(r => r.Id == command.Id, cancellationToken: ct);
        role.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleNotFound);

        roleRepository.Delete(role!);
        await roleRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(role!.UserPoolId, ct);

        return new DeleteRoleResponse(role!.Id);
    }
}
