using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.Roles.GetList;

public class GetListRoleEndpoint : EndpointWithoutRequest<ListModel<GetListRoleResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<RolesEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuzun rollerini listeler";
            s.Description = "Rolleri kapsamlarıyla birlikte dinamik filtre ve sayfalama ile döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListRoleQuery(Query<Guid>("UserPoolId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListRoleResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListRoleQuery(Guid UserPoolId) : BaseDynamicRequest, ICommand<ListModel<GetListRoleResponse>>;

public record GetListRoleResponse(
    Guid Id,
    string Key,
    string DisplayName,
    string? Description,
    List<RoleScopeResponse> RoleScopes,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime);

public record RoleScopeResponse(string Scope);

public class GetListRoleQueryHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository)
    : ICommandHandler<GetListRoleQuery, ListModel<GetListRoleResponse>>
{
    public async Task<ListModel<GetListRoleResponse>> ExecuteAsync(GetListRoleQuery command, CancellationToken ct)
    {
        var data = await roleRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: r => r.UserPoolId == command.UserPoolId,
            include: q => q.Include(r => r.RoleScopes.OrderBy(s => s.Scope)),
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            enableTracking: false,
            cancellationToken: ct);

        return data.MapListModel<Role, GetListRoleResponse>(command);
    }
}
