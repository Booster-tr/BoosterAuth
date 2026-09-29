using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Management.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.PoolUsers.UpdateAccess;

public class UpdatePoolUserAccessEndpoint : Endpoint<UpdatePoolUserAccessCommand, UpdatePoolUserAccessResponse>
{
    public override void Configure()
    {
        Put("pools/users/access");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuz kullanıcısının rol ve kapsamlarını değiştirir";
            s.Description = "Yalnızca havuzda tanımlı roller ve kapsamlar verilebilir. Liste bütün olarak değiştirilir.";
        });
    }

    public override async Task HandleAsync(UpdatePoolUserAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdatePoolUserAccessCommand(
    Guid UserPoolId,
    Guid PrincipalId,
    List<Guid> RoleIds,
    List<string> Scopes) : ICommand<UpdatePoolUserAccessResponse>;

public readonly record struct UpdatePoolUserAccessResponse(Guid PrincipalId);

public class UpdatePoolUserAccessCommandHandler(
    ClientDataService.ClientDataServiceClient clientDataClient,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<UpdatePoolUserAccessCommand, UpdatePoolUserAccessResponse>
{
    public async Task<UpdatePoolUserAccessResponse> ExecuteAsync(UpdatePoolUserAccessCommand command, CancellationToken ct)
    {
        var definitions = await clientDataClient.GetPoolDefinitionsAsync(
            new GetPoolDefinitionsRequest { UserPoolId = command.UserPoolId.ToString() }, cancellationToken: ct);

        if (!definitions.Found)
            throw new BusinessException(IdpMessageKeys.PoolNotFound);

        var poolRoles = definitions.Roles.ToDictionary(r => Guid.Parse(r.Id), r => r.Key);
        var roleIds = command.RoleIds.Distinct().ToList();

        if (roleIds.Any(id => !poolRoles.ContainsKey(id)))
            throw new BusinessException(IdpMessageKeys.RoleNotInPool);

        var scopes = command.Scopes.Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();

        if (scopes.Any(s => !definitions.Scopes.Contains(s)))
            throw new BusinessException(IdpMessageKeys.ScopeNotInPool);

        using var _ = requestContextAccessor.BeginSystemContext(command.UserPoolId);

        var exists = await principalRepository.AnyAsync(
            p => p.Id == command.PrincipalId && p.Type == PrincipalType.User, cancellationToken: ct);

        if (!exists)
            throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        await PrincipalAccessWriter.ReplaceAsync(
            principalRoleRepository, principalScopeRepository, command.PrincipalId,
            roleIds.Select(id => (id, poolRoles[id])).ToList(), scopes, ct);

        await principalRepository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(command.PrincipalId), ct);

        return new UpdatePoolUserAccessResponse(command.PrincipalId);
    }
}

public class UpdatePoolUserAccessValidator : Validator<UpdatePoolUserAccessCommand>
{
    public UpdatePoolUserAccessValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.RoleIds).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
        RuleFor(x => x.Scopes).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}

public static class PrincipalAccessWriter
{
    public static async Task ReplaceAsync(
        IAsyncRepository<PrincipalRole, IdpDbContext> roleRepository,
        IAsyncRepository<PrincipalScope, IdpDbContext> scopeRepository,
        Guid principalId,
        IReadOnlyList<(Guid Id, string Name)> roles,
        IReadOnlyList<string> scopes,
        CancellationToken ct)
    {
        foreach (var role in roleRepository.Query().Where(r => r.PrincipalId == principalId).ToList())
            roleRepository.Delete(role, softDelete: false);

        foreach (var scope in scopeRepository.Query().Where(s => s.PrincipalId == principalId).ToList())
            scopeRepository.Delete(scope, softDelete: false);

        foreach (var role in roles)
        {
            await roleRepository.AddAsync(new PrincipalRole
            {
                Id = EntityId.New(),
                PrincipalId = principalId,
                RoleId = role.Id,
                RoleName = role.Name,
                CreatedTime = DateTimeOffset.UtcNow
            }, ct);
        }

        foreach (var scope in scopes)
        {
            await scopeRepository.AddAsync(new PrincipalScope
            {
                Id = EntityId.New(),
                PrincipalId = principalId,
                Scope = scope,
                CreatedTime = DateTimeOffset.UtcNow
            }, ct);
        }
    }
}
