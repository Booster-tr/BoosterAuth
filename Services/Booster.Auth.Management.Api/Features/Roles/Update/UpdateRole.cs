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

namespace Booster.Auth.Management.Api.Features.Roles.Update;

public class UpdateRoleEndpoint : Endpoint<UpdateRoleCommand, UpdateRoleResponse>
{
    public override void Configure()
    {
        Put("");
        Group<RolesEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Rolü günceller";
            s.Description = "Anahtar, görünen ad, açıklama ve kapsamları günceller. Kullanıcılara daha önce " +
                            "atanmış rol adı IdP'de gRPC ile (yanıt beklenmeden) güncellenir.";
        });
    }

    public override async Task HandleAsync(UpdateRoleCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateRoleCommand(
    Guid UserPoolId,
    Guid RoleId,
    string Key,
    string DisplayName,
    string? Description,
    List<string> Scopes) : ICommand<UpdateRoleResponse>;

public readonly record struct UpdateRoleResponse(Guid Id);

public class UpdateRoleCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    IClientDataCacheStore clientDataCacheStore,
    IdpPoolDefinitionSync idpSync)
    : ICommandHandler<UpdateRoleCommand, UpdateRoleResponse>
{
    public async Task<UpdateRoleResponse> ExecuteAsync(UpdateRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetAsync(
            r => r.Id == command.RoleId && r.UserPoolId == command.UserPoolId, cancellationToken: ct);
        role.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleNotFound);

        var key = command.Key.Trim().ToLowerInvariant();
        var keyChanged = key != role!.Key;

        if (keyChanged)
        {
            var keyExists = await roleRepository.AnyAsync(
                r => r.UserPoolId == command.UserPoolId && r.Key == key && r.Id != role.Id, cancellationToken: ct);

            if (keyExists)
                throw new BusinessException(AuthNMessageKeys.RoleKeyAlreadyExists);
        }

        var scopes = command.Scopes.Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();

        if (scopes.Count > 0)
        {
            var defined = await scopeDefinitionRepository.CountAsync(
                s => s.UserPoolId == command.UserPoolId && scopes.Contains(s.Scope), cancellationToken: ct);

            if (defined != scopes.Count)
                throw new BusinessException(AuthNMessageKeys.ScopeNotDefinedInPool);
        }

        role.Key = key;
        role.DisplayName = command.DisplayName.Trim();
        role.Description = command.Description?.Trim();

        var current = await roleScopeRepository.Query()
            .Where(s => s.RoleId == role.Id)
            .ToListAsync(ct);

        foreach (var removed in current.Where(s => !scopes.Contains(s.Scope)))
            roleScopeRepository.Delete(removed, softDelete: false);

        foreach (var scope in scopes.Where(s => current.All(c => c.Scope != s)))
        {
            var roleScope = new RoleScope { RoleId = role.Id, Scope = scope };
            roleScope.SetId();
            await roleScopeRepository.AddAsync(roleScope, ct);
        }

        await roleRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(command.UserPoolId, ct);

        if (keyChanged)
            idpSync.RoleChanged(command.UserPoolId, role.Id, key);

        return new UpdateRoleResponse(role.Id);
    }
}

public class UpdateRoleValidator : Validator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.RoleId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

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
