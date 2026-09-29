using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Clients.Access.GetList;

public class GetListClientAccessEndpoint : EndpointWithoutRequest<AccessListResponse>
{
    public override void Configure()
    {
        Get("access");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Uygulama üzerindeki yetkileri listeler";
            s.Description = "Yalnızca doğrudan verilmiş panel yetkileri döner. " +
                            "Sayfalama devam anahtarıyla yapılır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetListClientAccessQuery(
            Query<Guid>("ClientId"),
            Query<int?>("PageSize", isRequired: false) ?? 20,
            Query<string>("ContinuationToken", isRequired: false));

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListClientAccessQuery(Guid ClientId, int PageSize, string? ContinuationToken)
    : ICommand<AccessListResponse>;

public class GetListClientAccessQueryHandler(IAccessManager accessManager)
    : ICommandHandler<GetListClientAccessQuery, AccessListResponse>
{
    public async Task<AccessListResponse> ExecuteAsync(GetListClientAccessQuery command, CancellationToken ct)
    {
        var page = await accessManager.ListAsync(
            AuthConstants.ClientType, command.ClientId, AccessRules.Assignable,
            command.PageSize, command.ContinuationToken, ct);

        return page.Adapt<AccessListResponse>();
    }
}
