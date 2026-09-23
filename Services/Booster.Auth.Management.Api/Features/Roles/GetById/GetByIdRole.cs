using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Roles.GetById;

public class GetByIdRoleEndpoint : EndpointWithoutRequest<GetByIdRoleResponse>
{
    public override void Configure()
    {
        Get("{id:guid}");
        Group<RolesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Rolü getirir";
            s.Description = "Id ile tek bir rolün detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdRoleQuery(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record GetByIdRoleQuery(Guid Id) : ICommand<GetByIdRoleResponse>;

public readonly record struct GetByIdRoleResponse(
    Guid Id,
    Guid UserPoolId,
    string Key,
    string DisplayName,
    string? Description);

public class GetByIdRoleQueryHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository)
    : ICommandHandler<GetByIdRoleQuery, GetByIdRoleResponse>
{
    public async Task<GetByIdRoleResponse> ExecuteAsync(GetByIdRoleQuery command, CancellationToken ct)
    {
        var role = await roleRepository.GetAsync(
            r => r.Id == command.Id, enableTracking: false, cancellationToken: ct);
        role.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleNotFound);

        return role.Adapt<GetByIdRoleResponse>();
    }
}
