using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.Clients.AllowedScopes;

public class UpdateClientAllowedScopesEndpoint
    : Endpoint<UpdateClientAllowedScopesCommand, UpdateClientAllowedScopesResponse>
{
    public override void Configure()
    {
        Put("allowed-scopes");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Uygulamanın isteyebileceği kapsamları belirler";
            s.Description = "Yalnızca uygulamanın havuzunda tanımlı kapsamlar seçilebilir. Liste bütün olarak değiştirilir.";
        });
    }

    public override async Task HandleAsync(UpdateClientAllowedScopesCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateClientAllowedScopesCommand(Guid ClientId, List<string> Scopes)
    : ICommand<UpdateClientAllowedScopesResponse>;

public readonly record struct UpdateClientAllowedScopesResponse(Guid Id);

public class UpdateClientAllowedScopesCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAsyncRepository<ClientAllowedScope, AuthNDbContext> allowedScopeRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateClientAllowedScopesCommand, UpdateClientAllowedScopesResponse>
{
    public async Task<UpdateClientAllowedScopesResponse> ExecuteAsync(
        UpdateClientAllowedScopesCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == command.ClientId, enableTracking: false, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        if (client!.UserPoolId == AuthConstants.ManagementPoolId)
            throw new BusinessException(AuthNMessageKeys.ManagementPoolClientsReserved);

        var scopes = command.Scopes.Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();

        if (scopes.Count > 0)
        {
            var defined = await scopeDefinitionRepository.CountAsync(
                s => s.UserPoolId == client!.UserPoolId && scopes.Contains(s.Scope), cancellationToken: ct);

            if (defined != scopes.Count)
                throw new BusinessException(AuthNMessageKeys.ScopeNotDefinedInPool);
        }

        var current = await allowedScopeRepository.Query()
            .Where(s => s.ClientId == command.ClientId)
            .ToListAsync(ct);

        foreach (var removed in current.Where(s => !scopes.Contains(s.Scope)))
            allowedScopeRepository.Delete(removed, softDelete: false);

        foreach (var scope in scopes.Where(s => current.All(c => c.Scope != s)))
        {
            var allowed = new ClientAllowedScope { ClientId = command.ClientId, Scope = scope };
            allowed.SetId();
            await allowedScopeRepository.AddAsync(allowed, ct);
        }

        await allowedScopeRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidateClientAsync(client!.ClientKey, ct);

        return new UpdateClientAllowedScopesResponse(command.ClientId);
    }
}

public class UpdateClientAllowedScopesValidator : Validator<UpdateClientAllowedScopesCommand>
{
    public UpdateClientAllowedScopesValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Scopes).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
