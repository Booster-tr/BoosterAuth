using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Helpers;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.Clients.GetList;

public class GetListClientEndpoint : EndpointWithoutRequest<ListModel<GetListClientResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope)));

        Summary(s =>
        {
            s.Summary = "Clientları listeler";
            s.Description = "Uygulamaları dinamik filtre ve sayfalama ile döner. " +
                            "Havuz kapsamı için query string içine userPoolId filtresi verilir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListClientQuery
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListClientResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListClientQuery : BaseDynamicRequest, ICommand<ListModel<GetListClientResponse>>;

public record GetListClientResponse(
    Guid Id,
    Guid UserPoolId,
    string ClientKey,
    string DisplayName,
    EntityStatus Status,
    OAuthGrantType AllowedGrantTypes,
    bool RequireConsent);

public class GetListClientQueryHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAccessManager accessManager)
    : ICommandHandler<GetListClientQuery, ListModel<GetListClientResponse>>
{
    public async Task<ListModel<GetListClientResponse>> ExecuteAsync(
        GetListClientQuery command, CancellationToken ct)
    {
        var accessible = await accessManager.ListAccessibleIdsAsync(AuthConstants.ClientType, AuthConstants.ViewerRelation, ct);

        var data = await clientRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: c => accessible.Contains(c.Id),
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<Client, GetListClientResponse>(command);
    }
}
