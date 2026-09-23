using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.RoleScopes.Remove;

public class RemoveRoleScopeEndpoint : EndpointWithoutRequest<RemoveRoleScopeResponse>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<RoleScopesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Role scope kaldırır";
            s.Description = "Bir role eklenmiş izin kaydını soft-delete ile kaldırır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new RemoveRoleScopeCommand(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record RemoveRoleScopeCommand(Guid Id) : ICommand<RemoveRoleScopeResponse>;

public readonly record struct RemoveRoleScopeResponse(Guid Id);

public class RemoveRoleScopeCommandHandler(
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository)
    : ICommandHandler<RemoveRoleScopeCommand, RemoveRoleScopeResponse>
{
    public async Task<RemoveRoleScopeResponse> ExecuteAsync(RemoveRoleScopeCommand command, CancellationToken ct)
    {
        var entity = await roleScopeRepository.GetAsync(x => x.Id == command.Id, cancellationToken: ct);
        entity.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleScopeNotFound);

        roleScopeRepository.Delete(entity!);
        await roleScopeRepository.SaveChangesAsync(ct);

        return new RemoveRoleScopeResponse(entity!.Id);
    }
}
