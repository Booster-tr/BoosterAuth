using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Database.Helpers;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.UserPools.GetList;

// Yalnızca çağıranın en az viewer olduğu havuzlar döner. Firma yetkisi
// ŞART DEĞİL: yalnızca havuza yetkisi olan biri de havuzunu görebilmeli.
public class GetListUserPoolEndpoint : EndpointWithoutRequest<ListModel<GetListUserPoolResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope)));

        Summary(s =>
        {
            s.Summary = "Kullanıcı havuzlarını listeler";
            s.Description = "Havuzları dinamik filtre ve sayfalama ile döner. " +
                            "Tenant kapsamı için query string'e tenantId filtresi verilir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListUserPoolQuery
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListUserPoolResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListUserPoolQuery : BaseDynamicRequest, ICommand<ListModel<GetListUserPoolResponse>>;

public record GetListUserPoolResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    EntityStatus Status);

public class GetListUserPoolQueryHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAccessManager accessManager)
    : ICommandHandler<GetListUserPoolQuery, ListModel<GetListUserPoolResponse>>
{
    public async Task<ListModel<GetListUserPoolResponse>> ExecuteAsync(GetListUserPoolQuery command, CancellationToken ct)
    {
        var accessible = await accessManager.ListAccessibleIdsAsync(AuthConstants.PoolType, AuthConstants.ViewerRelation, ct);

        var data = await userPoolRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: p => accessible.Contains(p.Id),
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<UserPool, GetListUserPoolResponse>(command);
    }
}
