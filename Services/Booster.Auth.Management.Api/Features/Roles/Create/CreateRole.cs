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
using Mapster;

namespace Booster.Auth.Management.Api.Features.Roles.Create;

public class CreateRoleEndpoint : Endpoint<CreateRoleCommand, CreateRoleResponse>
{
    public override void Configure()
    {
        Post("");
        Group<RolesEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Yeni rol oluşturur";
            s.Description = "Havuz altında rol tanımlar. Anahtar havuz içinde benzersizdir; " +
                            "kapsamlar yalnızca havuzun kapsam tanımlarından seçilebilir.";
        });
    }

    public override async Task HandleAsync(CreateRoleCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateRoleCommand(
    Guid UserPoolId,
    string Key,
    string DisplayName,
    string? Description,
    List<string> Scopes) : ICommand<CreateRoleResponse>;

public readonly record struct CreateRoleResponse(Guid Id);

public class CreateRoleCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<CreateRoleCommand, CreateRoleResponse>
{
    public async Task<CreateRoleResponse> ExecuteAsync(CreateRoleCommand command, CancellationToken ct)
    {
        var poolExists = await userPoolRepository.AnyAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);

        if (!poolExists)
            throw new BusinessException(AuthNMessageKeys.UserPoolNotFound);

        var key = command.Key.Trim().ToLowerInvariant();

        var keyExists = await roleRepository.AnyAsync(
            r => r.UserPoolId == command.UserPoolId && r.Key == key, cancellationToken: ct);

        if (keyExists)
            throw new BusinessException(AuthNMessageKeys.RoleKeyAlreadyExists);

        var scopes = command.Scopes.Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();

        if (scopes.Count > 0)
        {
            var defined = await scopeDefinitionRepository.CountAsync(
                s => s.UserPoolId == command.UserPoolId && scopes.Contains(s.Scope), cancellationToken: ct);

            if (defined != scopes.Count)
                throw new BusinessException(AuthNMessageKeys.ScopeNotDefinedInPool);
        }

        var role = command.Adapt<Role>();
        role.Key = key;
        role.SetId();

        foreach (var scope in scopes)
        {
            var roleScope = new RoleScope { RoleId = role.Id, Scope = scope };
            roleScope.SetId();
            role.RoleScopes.Add(roleScope);
        }

        await roleRepository.AddAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(command.UserPoolId, ct);

        return new CreateRoleResponse(role.Id);
    }
}

public class CreateRoleValidator : Validator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-z0-9-]+$").WithMessage(AuthNMessageKeys.RoleKeyInvalidFormat);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Description).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Scopes).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
