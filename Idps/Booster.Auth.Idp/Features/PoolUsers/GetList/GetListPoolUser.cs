using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Idp.Features.PoolUsers.GetList;

public class GetListPoolUserEndpoint : EndpointWithoutRequest<ListModel<PoolUserResponse>>
{
    public override void Configure()
    {
        Get("pools/users");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s => s.Summary = "Havuzun kullanıcılarını listeler");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListPoolUserQuery(Query<Guid>("UserPoolId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<PoolUserResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListPoolUserQuery(Guid UserPoolId) : BaseDynamicRequest, ICommand<ListModel<PoolUserResponse>>;

public record PoolUserResponse(
    Guid Id,
    string DisplayName,
    string? UserName,
    string? Email,
    string? PhoneNumber,
    PrincipalStatus Status,
    DateTimeOffset CreatedTime);

public class GetListPoolUserQueryHandler(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GetListPoolUserQuery, ListModel<PoolUserResponse>>
{
    public async Task<ListModel<PoolUserResponse>> ExecuteAsync(GetListPoolUserQuery command, CancellationToken ct)
    {
      using var _ = requestContextAccessor.BeginSystemContext(command.UserPoolId);

        var data = await principalRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: p => p.Type == PrincipalType.User,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            enableTracking: false,
            cancellationToken: ct);

        return data.MapListModel<Principal, PoolUserResponse>(command);
    }
}
