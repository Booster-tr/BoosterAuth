using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.GetList;

public class GetListPoolServiceAccountEndpoint : EndpointWithoutRequest<ListModel<PoolServiceAccountResponse>>
{
    public override void Configure()
    {
        Get("pools/service-accounts");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s => s.Summary = "Havuz için açılmış servis hesaplarını listeler");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListPoolServiceAccountQuery(Query<Guid>("UserPoolId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<PoolServiceAccountResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListPoolServiceAccountQuery(Guid UserPoolId)
    : BaseDynamicRequest, ICommand<ListModel<PoolServiceAccountResponse>>;

public record PoolServiceAccountResponse(
    Guid Id,
    string Name,
    string Key,
    ServiceAccountStatus Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedTime);

public class GetListPoolServiceAccountQueryHandler(
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GetListPoolServiceAccountQuery, ListModel<PoolServiceAccountResponse>>
{
    public async Task<ListModel<PoolServiceAccountResponse>> ExecuteAsync(
        GetListPoolServiceAccountQuery command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var data = await serviceAccountRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: s => s.OwnerPoolId == command.UserPoolId,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            enableTracking: false,
            cancellationToken: ct);

        return data.MapListModel<ServiceAccount, PoolServiceAccountResponse>(command);
    }
}
