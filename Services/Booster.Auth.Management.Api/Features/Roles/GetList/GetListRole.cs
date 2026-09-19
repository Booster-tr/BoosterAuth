using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.N.Extensions;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Roles.GetList;

public class GetListRoleEndpoint : EndpointWithoutRequest<ListModel<GetListRoleResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<RolesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Rolleri listeler";
            s.Description = "Rolleri dinamik filtre ve sayfalama ile döner. " +
                            "Havuz kapsamı için query string içine userPoolId filtresi verilir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListRoleQuery
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListRoleResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListRoleQuery : BaseDynamicRequest, ICommand<ListModel<GetListRoleResponse>>;

public record GetListRoleResponse(
    Guid Id,
    Guid UserPoolId,
    string Key,
    string DisplayName,
    string? Description);

public class GetListRoleQueryHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository)
    : ICommandHandler<GetListRoleQuery, ListModel<GetListRoleResponse>>
{
    public async Task<ListModel<GetListRoleResponse>> ExecuteAsync(GetListRoleQuery command, CancellationToken ct)
    {
        var data = await roleRepository.ListDynamicAsync(
            command.DynamicQuery,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<Role, GetListRoleResponse>(command);
    }
}
