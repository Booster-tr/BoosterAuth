using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.Delete;

public class DeleteScopeDefinitionEndpoint : EndpointWithoutRequest<DeleteScopeDefinitionResponse>
{
    public override void Configure()
    {
        Delete("");
        Group<ScopeDefinitionsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Kapsam tanımını siler";
            s.Description = "Tanımı soft-delete ile kaldırır; aynı havuzdaki rol/uygulama atamalarını " +
                            "ve havuzun varsayılan kapsamlarındaki karşılığını da temizler.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteScopeDefinitionCommand(
            Query<Guid>("UserPoolId"), Query<Guid>("ScopeDefinitionId")).ExecuteAsync(ct), ct);
}

public record DeleteScopeDefinitionCommand(Guid UserPoolId, Guid ScopeDefinitionId) : ICommand<DeleteScopeDefinitionResponse>;

public readonly record struct DeleteScopeDefinitionResponse(Guid Id);

public class DeleteScopeDefinitionCommandHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository,
    IAsyncRepository<ClientAllowedScope, AuthNDbContext> clientAllowedScopeRepository,
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<DeleteScopeDefinitionCommand, DeleteScopeDefinitionResponse>
{
    public async Task<DeleteScopeDefinitionResponse> ExecuteAsync(
        DeleteScopeDefinitionCommand command, CancellationToken ct)
    {
        var scope = await scopeRepository.GetAsync(
            s => s.Id == command.ScopeDefinitionId && s.UserPoolId == command.UserPoolId, cancellationToken: ct);
        scope.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ScopeDefinitionNotFound);

        scopeRepository.Delete(scope!);

        var poolId = scope!.UserPoolId;
        var scopeKey = scope.Scope;

        // Atama tabloları havuz bilgisini taşımaz; kapsam ilişki üzerinden daraltılır.
        var clientScopes = await clientAllowedScopeRepository.Query()
            .Where(x => x.Client.UserPoolId == poolId && x.Scope == scopeKey)
            .ToListAsync(ct);

        foreach (var row in clientScopes)
            clientAllowedScopeRepository.Delete(row, softDelete: false);

        var roleScopes = await roleScopeRepository.Query()
            .Where(x => x.Role.UserPoolId == poolId && x.Scope == scopeKey)
            .ToListAsync(ct);

        foreach (var row in roleScopes)
            roleScopeRepository.Delete(row, softDelete: false);

        var pool = await userPoolRepository.GetAsync(p => p.Id == poolId, cancellationToken: ct);

        if (pool is not null && pool.DefaultScopes.Contains(scopeKey))
            pool.DefaultScopes = pool.DefaultScopes.Where(s => s != scopeKey).ToList();

        await scopeRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(poolId, ct);

        return new DeleteScopeDefinitionResponse(scope.Id);
    }
}
