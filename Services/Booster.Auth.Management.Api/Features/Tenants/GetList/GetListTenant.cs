using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Helpers;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Tenants.GetList;

public class GetListTenantEndpoint : EndpointWithoutRequest<ListModel<GetListTenantResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<TenantsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Tenant listeler";
            s.Description = "Kiracıları dinamik filtre ve sayfalama ile döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListTenantQuery
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListTenantResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListTenantQuery : BaseDynamicRequest, ICommand<ListModel<GetListTenantResponse>>;

public record GetListTenantResponse(Guid Id, string Name);

public class GetListTenantQueryHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<GetListTenantQuery, ListModel<GetListTenantResponse>>
{
    public async Task<ListModel<GetListTenantResponse>> ExecuteAsync(GetListTenantQuery command, CancellationToken ct)
    {
        var data = await tenantRepository.ListDynamicAsync(
            command.DynamicQuery,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<Tenant, GetListTenantResponse>(command);
    }
}
