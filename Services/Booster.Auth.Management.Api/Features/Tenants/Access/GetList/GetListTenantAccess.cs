using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Tenants.Access.GetList;

public class GetListTenantAccessEndpoint : EndpointWithoutRequest<GetListTenantAccessResponse>
{
    public override void Configure()
    {
        Get("access");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Firma üzerindeki yetkileri listeler";
            s.Description = "Yalnızca doğrudan verilmiş yetkiler döner. Sayfalama devam anahtarıyla yapılır: " +
                            "yanıttaki ContinuationToken bir sonraki isteğe verilir; boşsa son sayfadır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetListTenantAccessQuery(
            Query<Guid>("TenantId"),
            Query<int?>("PageSize", isRequired: false) ?? 20,
            Query<string>("ContinuationToken", isRequired: false));

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListTenantAccessQuery(Guid TenantId, int PageSize, string? ContinuationToken)
    : ICommand<GetListTenantAccessResponse>;

public record GetListTenantAccessResponse(
    IReadOnlyList<TenantAccessResponse> Items,
    string? ContinuationToken);

public readonly record struct TenantAccessResponse(Guid PrincipalId, string Relation);

public class GetListTenantAccessQueryHandler(IAccessManager accessManager)
    : ICommandHandler<GetListTenantAccessQuery, GetListTenantAccessResponse>
{
    public async Task<GetListTenantAccessResponse> ExecuteAsync(
        GetListTenantAccessQuery command, CancellationToken ct)
    {
        var page = await accessManager.ListAsync(
            AuthConstants.TenantType, command.TenantId, command.PageSize, command.ContinuationToken, ct);

        return page.Adapt<GetListTenantAccessResponse>();
    }
}
