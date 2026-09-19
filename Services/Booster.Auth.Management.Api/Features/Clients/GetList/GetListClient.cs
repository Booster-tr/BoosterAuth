using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Helpers;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Clients.GetList;

public class GetListClientEndpoint : EndpointWithoutRequest<ListModel<GetListClientResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<ClientsEndpointGroup>();
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
    IAsyncRepository<Client, AuthNDbContext> clientRepository)
    : ICommandHandler<GetListClientQuery, ListModel<GetListClientResponse>>
{
    public async Task<ListModel<GetListClientResponse>> ExecuteAsync(
        GetListClientQuery command, CancellationToken ct)
    {
        var data = await clientRepository.ListDynamicAsync(
            command.DynamicQuery,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<Client, GetListClientResponse>(command);
    }
}
