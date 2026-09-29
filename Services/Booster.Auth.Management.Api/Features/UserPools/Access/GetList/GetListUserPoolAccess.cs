using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.UserPools.Access.GetList;

public class GetListUserPoolAccessEndpoint : EndpointWithoutRequest<AccessListResponse>
{
    public override void Configure()
    {
        Get("access");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuz üzerindeki yetkileri listeler";
            s.Description = "Yalnızca doğrudan verilmiş panel yetkileri döner; son kullanıcılar (end_user) listelenmez. " +
                            "Sayfalama devam anahtarıyla yapılır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetListUserPoolAccessQuery(
            Query<Guid>("UserPoolId"),
            Query<int?>("PageSize", isRequired: false) ?? 20,
            Query<string>("ContinuationToken", isRequired: false));

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListUserPoolAccessQuery(Guid UserPoolId, int PageSize, string? ContinuationToken)
    : ICommand<AccessListResponse>;

public class GetListUserPoolAccessQueryHandler(IAccessManager accessManager)
    : ICommandHandler<GetListUserPoolAccessQuery, AccessListResponse>
{
    public async Task<AccessListResponse> ExecuteAsync(GetListUserPoolAccessQuery command, CancellationToken ct)
    {
        var page = await accessManager.ListAsync(
            AuthConstants.PoolType, command.UserPoolId, AccessRules.Assignable,
            command.PageSize, command.ContinuationToken, ct);

        return page.Adapt<AccessListResponse>();
    }
}
