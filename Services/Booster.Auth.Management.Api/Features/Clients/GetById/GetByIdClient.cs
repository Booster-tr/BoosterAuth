using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.Clients.GetById;

public class GetByIdClientEndpoint : EndpointWithoutRequest<GetByIdClientResponse>
{
    public override void Configure()
    {
        Get("detail");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.ViewerRelation)));
        Summary(s =>
        {
            s.Summary = "Client getirir";
            s.Description = "Id ile tek bir uygulamanın detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdClientQuery(Query<Guid>("ClientId")).ExecuteAsync(ct), ct);
}

public record GetByIdClientQuery(Guid ClientId) : ICommand<GetByIdClientResponse>;

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
    List<TokenScopePolicyItem> TokenScopePolicy,
    string UserPoolName,
    IReadOnlyList<ClientScopeResponse> AllowedScopes,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime)
{
    /// <summary>Havuzda tanımlı kapsamlar: izin verilen kapsamlar bunlar arasından seçilir.</summary>
    public IReadOnlyList<ClientPoolScopeResponse> PoolScopes { get; init; } = [];
}

public record ClientScopeResponse(string Scope);

public record ClientPoolScopeResponse(string Scope, string DisplayName);

public class GetByIdClientQueryHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository)
    : ICommandHandler<GetByIdClientQuery, GetByIdClientResponse>
{
    public async Task<GetByIdClientResponse> ExecuteAsync(GetByIdClientQuery command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == command.ClientId,
            include: q => q
                .Include(c => c.AllowedScopes.OrderBy(s => s.Scope))
                .Include(c => c.UserPool)
                .ThenInclude(p => p.ScopeDefinitions.OrderBy(s => s.Scope)),
            enableTracking: false,
            cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        return client!.Adapt<GetByIdClientResponse>() with
        {
            PoolScopes = client.UserPool.ScopeDefinitions.Adapt<List<ClientPoolScopeResponse>>()
        };
    }
}
