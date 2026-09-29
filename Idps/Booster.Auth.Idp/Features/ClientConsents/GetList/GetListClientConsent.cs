using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.ClientConsents.GetList;

public class GetListClientConsentEndpoint : EndpointWithoutRequest<ListModel<ClientConsentResponse>>
{
    public override void Configure()
    {
        Get("clients/consents");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, IdpPolicies.ClientTarget, AuthConstants.ViewerRelation)));

        Summary(s => s.Summary = "Uygulamaya verilmiş geçerli kullanıcı onaylarını listeler");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListClientConsentQuery(Query<Guid>("ClientId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<ClientConsentResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListClientConsentQuery(Guid ClientId) : BaseDynamicRequest, ICommand<ListModel<ClientConsentResponse>>;

public record ClientConsentResponse(
    Guid Id,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string? PrincipalEmail,
    string GrantedScopes,
    DateTimeOffset CreatedTime,
    DateTimeOffset? ExpiresAt);

public class GetListClientConsentQueryHandler(IAsyncRepository<Consent, IdpDbContext> consentRepository)
    : ICommandHandler<GetListClientConsentQuery, ListModel<ClientConsentResponse>>
{
    public async Task<ListModel<ClientConsentResponse>> ExecuteAsync(GetListClientConsentQuery command, CancellationToken ct)
    {
        // Onaylar uygulamanın havuzunun izolasyonunda durur; istemci kimliği tekil
        // olduğu için izolasyon filtresi yerine doğrudan istemciye göre süzülür.
        var data = await consentRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: c => c.ClientId == command.ClientId
                            && c.DeletedTime == null
                            && c.RevokedAt == null
                            && (c.ExpiresAt == null || c.ExpiresAt > DateTimeOffset.UtcNow),
            include: q => q.Include(c => c.Principal),
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            withoutGlobalFilter: true,
            enableTracking: false,
            cancellationToken: ct);

        return data.MapListModel<Consent, ClientConsentResponse>(command);
    }
}
