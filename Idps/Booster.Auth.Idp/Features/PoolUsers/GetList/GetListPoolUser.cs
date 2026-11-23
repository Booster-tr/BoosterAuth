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
using Microsoft.EntityFrameworkCore;

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
    DateTimeOffset CreatedTime)
{
    /// <summary>Parolasini henuz belirlememis (davet bekleyen) kullanici. Federasyonla gelen kullanicilarda da parola yoktur; onlar kimlik saglayicisi baglantisi ile ayrilir.</summary>
    public bool InvitationPending { get; init; }
}

public class GetListPoolUserQueryHandler(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IAsyncRepository<PrincipalIdentity, IdpDbContext> identityRepository,
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

        var list = data.MapListModel<Principal, PoolUserResponse>(command);

        // Davet bekleyen = yerel parolasi YOK ve dis kimlik saglayicisi baglantisi YOK.
        var ids = list.Items.Select(u => u.Id).ToList();
        if (ids.Count == 0) return list;
        var withPassword = await credentialRepository.Query().AsNoTracking()
            .Where(c => ids.Contains(c.PrincipalId)).Select(c => c.PrincipalId).ToListAsync(ct);
        var federated = await identityRepository.Query().AsNoTracking()
            .Where(i => ids.Contains(i.PrincipalId)).Select(i => i.PrincipalId).ToListAsync(ct);
        var active = withPassword.Concat(federated).ToHashSet();
        list.Items = list.Items.Select(u => u with { InvitationPending = !active.Contains(u.Id) }).ToList();

        return list;
    }
}
