using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Tenants.Access.GetList;

public class GetListTenantAccessEndpoint : EndpointWithoutRequest<ListModel<TenantAccessResponse>>
{
    public override void Configure()
    {
        Get("access");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Firma üzerindeki yetkileri listeler";
            s.Description = "Yalnızca doğrudan verilmiş yetkiler döner; türetilmiş yetkiler listelenmez.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pageRequest = HttpContext.Request.ToNvc().ToPageRequest();

        var query = new GetListTenantAccessQuery(Query<Guid>("TenantId"), pageRequest);

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListTenantAccessQuery(Guid TenantId, PageRequest PageRequest)
    : ICommand<ListModel<TenantAccessResponse>>;

public readonly record struct TenantAccessResponse(
    Guid PrincipalId,
    IReadOnlyList<string> Relations);

public class GetListTenantAccessQueryHandler(IAccessManager accessManager)
    : ICommandHandler<GetListTenantAccessQuery, ListModel<TenantAccessResponse>>
{
    public async Task<ListModel<TenantAccessResponse>> ExecuteAsync(
        GetListTenantAccessQuery command, CancellationToken ct)
    {
        var entries = await accessManager.ListAsync(
            AuthConstants.TenantType, command.TenantId, ct);

        var index = command.PageRequest.PageIndex;
        var size = command.PageRequest.PageSize;

        var items = entries
            .Skip((index - 1) * size)
            .Take(size)
            .Select(entry => new TenantAccessResponse(entry.PrincipalId, entry.Relations))
            .ToList();

        return new ListModel<TenantAccessResponse>
        {
            Items = items,
            Count = entries.Count,
            Index = index,
            Size = size,
            Pages = size > 0 ? (int)Math.Ceiling(entries.Count / (double)size) : 0,
            HasPrevious = index > 1,
            HasNext = index * size < entries.Count,
            PageRequest = command.PageRequest
        };
    }
}
