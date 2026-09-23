using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.GetById;

public class GetByIdScopeDefinitionEndpoint : EndpointWithoutRequest<GetByIdScopeDefinitionResponse>
{
    public override void Configure()
    {
        Get("{id:guid}");
        Group<ScopeDefinitionsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Scope tanımını getirir";
            s.Description = "Id ile tek bir scope tanımının detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdScopeDefinitionQuery(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record GetByIdScopeDefinitionQuery(Guid Id) : ICommand<GetByIdScopeDefinitionResponse>;

public readonly record struct GetByIdScopeDefinitionResponse(
    Guid Id,
    Guid UserPoolId,
    string Scope,
    string DisplayName,
    string? Description);

public class GetByIdScopeDefinitionQueryHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository)
    : ICommandHandler<GetByIdScopeDefinitionQuery, GetByIdScopeDefinitionResponse>
{
    public async Task<GetByIdScopeDefinitionResponse> ExecuteAsync(
        GetByIdScopeDefinitionQuery command, CancellationToken ct)
    {
        var scope = await scopeRepository.GetAsync(
            s => s.Id == command.Id, enableTracking: false, cancellationToken: ct);
        scope.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ScopeDefinitionNotFound);

        return scope.Adapt<GetByIdScopeDefinitionResponse>();
    }
}
