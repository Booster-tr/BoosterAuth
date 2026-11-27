using Booster.Auth.Management.Api.Managers.PoolDefinitionSyncManagers;
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

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.Update;

public class UpdateScopeDefinitionEndpoint : Endpoint<UpdateScopeDefinitionCommand, UpdateScopeDefinitionResponse>
{
    public override void Configure()
    {
        Put("");
        Group<ScopeDefinitionsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Kapsam tanımını günceller";
            s.Description = "Kapsam adı, görünen ad ve açıklamayı günceller. Ad değişirse rol, uygulama ve havuz atamaları " +
                            "yeni ada taşınır; kullanıcılara atanmış kapsam IdP'de gRPC ile (yanıt beklenmeden) güncellenir.";
        });
    }

    public override async Task HandleAsync(UpdateScopeDefinitionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateScopeDefinitionCommand(
    Guid UserPoolId,
    Guid ScopeDefinitionId,
    string Scope,
    string DisplayName,
    string? Description) : ICommand<UpdateScopeDefinitionResponse>;

public readonly record struct UpdateScopeDefinitionResponse(Guid Id);

public class UpdateScopeDefinitionCommandHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository,
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository,
    IAsyncRepository<ClientAllowedScope, AuthNDbContext> clientAllowedScopeRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore,
    IdpPoolDefinitionSync idpSync)
    : ICommandHandler<UpdateScopeDefinitionCommand, UpdateScopeDefinitionResponse>
{
    public async Task<UpdateScopeDefinitionResponse> ExecuteAsync(
        UpdateScopeDefinitionCommand command, CancellationToken ct)
    {
        var scope = await scopeRepository.GetAsync(
            s => s.Id == command.ScopeDefinitionId && s.UserPoolId == command.UserPoolId, cancellationToken: ct);
        scope.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ScopeDefinitionNotFound);

        var poolId = command.UserPoolId;
        var oldName = scope!.Scope;
        var newName = command.Scope.Trim().ToLowerInvariant();

        if (newName != oldName)
        {
            var nameExists = await scopeRepository.AnyAsync(
                s => s.UserPoolId == poolId && s.Scope == newName && s.Id != scope.Id, cancellationToken: ct);

            if (nameExists)
                throw new BusinessException(AuthNMessageKeys.ScopeAlreadyDefined);

            // Ad havuz içinde benzersiz olduğu için yeni adı taşıyan başka atama yoktur; doğrudan yeniden adlandırılır.
            var roleScopes = await roleScopeRepository.Query()
                .Where(x => x.Role.UserPoolId == poolId && x.Scope == oldName)
                .ToListAsync(ct);

            foreach (var row in roleScopes)
                row.Scope = newName;

            var clientScopes = await clientAllowedScopeRepository.Query()
                .Where(x => x.Client.UserPoolId == poolId && x.Scope == oldName)
                .ToListAsync(ct);

            foreach (var row in clientScopes)
                row.Scope = newName;

            var pool = await userPoolRepository.GetAsync(p => p.Id == poolId, cancellationToken: ct);

            if (pool is not null && pool.DefaultScopes.Contains(oldName))
                pool.DefaultScopes = pool.DefaultScopes.Select(s => s == oldName ? newName : s).ToList();

            scope.Scope = newName;
        }

        scope.DisplayName = command.DisplayName.Trim();
        scope.Description = command.Description?.Trim();

        await scopeRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(poolId, ct);

        if (newName != oldName)
            idpSync.ScopeDefinitionChanged(poolId, oldName, newName);

        return new UpdateScopeDefinitionResponse(scope.Id);
    }
}

public class UpdateScopeDefinitionValidator : Validator<UpdateScopeDefinitionCommand>
{
    public UpdateScopeDefinitionValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ScopeDefinitionId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(200).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-zA-Z0-9:_.-]+$").WithMessage(AuthNMessageKeys.ScopeInvalidFormat);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Description).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
    }
}
