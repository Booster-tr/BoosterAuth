using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.N.Extensions;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.UserPools.GetList;

public class GetListUserPoolEndpoint : EndpointWithoutRequest<ListModel<GetListUserPoolResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<UserPoolsEndpointGroup>();
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
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<GetListUserPoolQuery, ListModel<GetListUserPoolResponse>>
{
    public async Task<ListModel<GetListUserPoolResponse>> ExecuteAsync(GetListUserPoolQuery command, CancellationToken ct)
    {
        var data = await userPoolRepository.ListDynamicAsync(
            command.DynamicQuery,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<UserPool, GetListUserPoolResponse>(command);
    }
}
