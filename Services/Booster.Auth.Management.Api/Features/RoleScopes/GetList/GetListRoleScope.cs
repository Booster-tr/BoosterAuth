using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.N.Extensions;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.RoleScopes.GetList;

public class GetListRoleScopeEndpoint : EndpointWithoutRequest<ListModel<GetListRoleScopeResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<RoleScopesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Role scope kayıtlarını listeler";
            s.Description = "Dinamik filtre ve sayfalama ile döner. " +
                            "Tek bir rolün izinleri için query string içine roleId filtresi verilir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListRoleScopeQuery
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListRoleScopeResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListRoleScopeQuery : BaseDynamicRequest, ICommand<ListModel<GetListRoleScopeResponse>>;

public record GetListRoleScopeResponse(Guid Id, Guid RoleId, string Scope);

public class GetListRoleScopeQueryHandler(
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository)
    : ICommandHandler<GetListRoleScopeQuery, ListModel<GetListRoleScopeResponse>>
{
    public async Task<ListModel<GetListRoleScopeResponse>> ExecuteAsync(
        GetListRoleScopeQuery command, CancellationToken ct)
    {
        var data = await roleScopeRepository.ListDynamicAsync(
            command.DynamicQuery,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<RoleScope, GetListRoleScopeResponse>(command);
    }
}
