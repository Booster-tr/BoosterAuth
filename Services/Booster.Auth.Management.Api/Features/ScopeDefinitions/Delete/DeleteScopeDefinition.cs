using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.Delete;

public class DeleteScopeDefinitionEndpoint : EndpointWithoutRequest<DeleteScopeDefinitionResponse>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<ScopeDefinitionsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Scope tanımını siler";
            s.Description = "Tanımı soft-delete ile kaldırır ve aynı havuzda bu izne yapılan " +
                            "rol/uygulama atamalarını da temizler. Tanım gidince atamaların karşılığı kalmaz.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteScopeDefinitionCommand(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record DeleteScopeDefinitionCommand(Guid Id) : ICommand<DeleteScopeDefinitionResponse>;

public readonly record struct DeleteScopeDefinitionResponse(Guid Id);

public class DeleteScopeDefinitionCommandHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository,
    IAsyncRepository<ClientAllowedScope, AuthNDbContext> clientAllowedScopeRepository,
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository)
    : ICommandHandler<DeleteScopeDefinitionCommand, DeleteScopeDefinitionResponse>
{
    public async Task<DeleteScopeDefinitionResponse> ExecuteAsync(
        DeleteScopeDefinitionCommand command, CancellationToken ct)
    {
        var scope = await scopeRepository.GetAsync(s => s.Id == command.Id, cancellationToken: ct);
        scope.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ScopeDefinitionNotFound);

        scopeRepository.Delete(scope!);

        var poolId = scope!.UserPoolId;
        var scopeKey = scope.Scope;

        // Atama tabloları havuz bilgisini taşımaz; kapsam ilişki üzerinden daraltılır.
        var clientScopes = await clientAllowedScopeRepository.Query()
            .Where(x => x.Client.UserPoolId == poolId && x.Scope == scopeKey)
            .ToListAsync(ct);

        foreach (var row in clientScopes)
            clientAllowedScopeRepository.Delete(row);

        var roleScopes = await roleScopeRepository.Query()
            .Where(x => x.Role.UserPoolId == poolId && x.Scope == scopeKey)
            .ToListAsync(ct);

        foreach (var row in roleScopes)
            roleScopeRepository.Delete(row);

        await scopeRepository.SaveChangesAsync(ct);

        return new DeleteScopeDefinitionResponse(scope.Id);
    }
}
