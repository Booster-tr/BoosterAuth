using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.ClientSessions.GetList;

public class GetListClientSessionEndpoint : EndpointWithoutRequest<ListModel<ClientSessionResponse>>
{
    public override void Configure()
    {
        Get("clients/sessions");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, IdpPolicies.ClientTarget, AuthConstants.ViewerRelation)));

        Summary(s => s.Summary = "Uygulamanın aktif oturumlarını listeler");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListClientSessionQuery(Query<Guid>("ClientId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<ClientSessionResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListClientSessionQuery(Guid ClientId) : BaseDynamicRequest, ICommand<ListModel<ClientSessionResponse>>;

public record ClientSessionResponse(
    Guid Id,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string? PrincipalEmail,
    string? DeviceName,
    string? UserAgent,
    string? IpAddress,
    string? AuthMethod,
    DateTimeOffset CreatedTime,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt);

public class GetListClientSessionQueryHandler(IAsyncRepository<Session, IdpDbContext> sessionRepository)
    : ICommandHandler<GetListClientSessionQuery, ListModel<ClientSessionResponse>>
{
    public async Task<ListModel<ClientSessionResponse>> ExecuteAsync(GetListClientSessionQuery command, CancellationToken ct)
    {
        // Oturumlar uygulamanın havuzunun izolasyonundadır; istemciye göre süzülür.
        var data = await sessionRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: s => s.ClientId == command.ClientId
                            && s.DeletedTime == null
                            && s.Status == SessionStatus.Active
                            && s.ExpiresAt > DateTimeOffset.UtcNow,
            include: q => q.Include(s => s.Principal),
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            withoutGlobalFilter: true,
            enableTracking: false,
            cancellationToken: ct);

        return data.MapListModel<Session, ClientSessionResponse>(command);
    }
}
