using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Clients.GetById;

public class GetByIdClientEndpoint : EndpointWithoutRequest<GetByIdClientResponse>
{
    public override void Configure()
    {
        Get("{id:guid}");
        Group<ClientsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Client getirir";
            s.Description = "Id ile tek bir uygulamanın detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdClientQuery(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record GetByIdClientQuery(Guid Id) : ICommand<GetByIdClientResponse>;

public record GetByIdClientResponse(
    Guid Id,
    Guid UserPoolId,
    string ClientKey,
    string DisplayName,
    string? LogoUri,
    EntityStatus Status,
    OAuthGrantType AllowedGrantTypes,
    List<string> RedirectUris,
    List<string> PostLogoutRedirectUris,
    List<string> AllowedAudiences,
    bool RequireConsent,
    List<TokenScopePolicyItem> TokenScopePolicy);

public class GetByIdClientQueryHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository)
    : ICommandHandler<GetByIdClientQuery, GetByIdClientResponse>
{
    public async Task<GetByIdClientResponse> ExecuteAsync(GetByIdClientQuery command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == command.Id, enableTracking: false, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        return client.Adapt<GetByIdClientResponse>();
    }
}
