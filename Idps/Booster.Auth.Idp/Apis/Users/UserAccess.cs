using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Apis._base;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Management.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Apis.Users;

// ----------------------------------------------------------------------------- okuma

public class GetUserAccessEndpoint : EndpointWithoutRequest<UserAccessResponse>
{
    public override void Configure()
    {
        Get($"{PoolApiRoutes.Users}/{{principalId}}/access");
        Summary(s =>
        {
            s.Summary = "Kullanıcının rol ve kapsamlarını döner (havuz API'si)";
            s.Description = "Havuzun servis hesabı token'ı ile çağrılır; havuz token'daki actor'dır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetUserAccessQuery(Route<Guid>("principalId")).ExecuteAsync(ct), ct);
}

public record GetUserAccessQuery(Guid PrincipalId) : ICommand<UserAccessResponse>;

public record UserAccessRole(Guid Id, string Key);

public record UserAccessResponse(IReadOnlyList<UserAccessRole> Roles, IReadOnlyList<string> Scopes);

public class GetUserAccessQueryHandler(
    IdpDbContext db,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GetUserAccessQuery, UserAccessResponse>
{
    public async Task<UserAccessResponse> ExecuteAsync(GetUserAccessQuery query, CancellationToken ct)
    {
        var poolId = await PoolApiCaller.RequirePoolAsync(requestContextAccessor, db, authorizationClient, AuthConstants.ViewerRelation, ct);

        using var _ = requestContextAccessor.BeginSystemContext(poolId);

        await UserAccessRules.EnsureUserAsync(db, query.PrincipalId, ct);

        return await UserAccessRules.ReadAsync(db, query.PrincipalId, ct);
    }
}

// ----------------------------------------------------------------------------- ekleme / kaldirma

public class GrantUserAccessEndpoint : Endpoint<ChangeUserAccessRequest, UserAccessResponse>
{
    public override void Configure()
    {
        Post($"{PoolApiRoutes.Users}/{{principalId}}/access/grant");
        Summary(s =>
        {
            s.Summary = "Kullanıcıya rol ve kapsam ekler (havuz API'si)";
            s.Description = "Yalnızca havuzda tanımlı roller ve kapsamlar verilebilir; zaten olanlar atlanır. " +
                            "Değişiklik kullanıcının bir sonraki token'ında görünür.";
        });
    }

    public override async Task HandleAsync(ChangeUserAccessRequest req, CancellationToken ct)
        => await Send.OkAsync(await new ChangeUserAccessCommand(Route<Guid>("principalId"), req.RoleIds, req.Scopes, Grant: true).ExecuteAsync(ct), ct);
}

public class RevokeUserAccessEndpoint : Endpoint<ChangeUserAccessRequest, UserAccessResponse>
{
    public override void Configure()
    {
        Post($"{PoolApiRoutes.Users}/{{principalId}}/access/revoke");
        Summary(s =>
        {
            s.Summary = "Kullanıcıdan rol ve kapsam kaldırır (havuz API'si)";
            s.Description = "Kullanıcıda olmayanlar atlanır. Değişiklik kullanıcının bir sonraki token'ında görünür.";
        });
    }

    public override async Task HandleAsync(ChangeUserAccessRequest req, CancellationToken ct)
        => await Send.OkAsync(await new ChangeUserAccessCommand(Route<Guid>("principalId"), req.RoleIds, req.Scopes, Grant: false).ExecuteAsync(ct), ct);
}

public record ChangeUserAccessRequest(List<Guid>? RoleIds, List<string>? Scopes);

public record ChangeUserAccessCommand(Guid PrincipalId, List<Guid>? RoleIds, List<string>? Scopes, bool Grant)
    : ICommand<UserAccessResponse>;

public class ChangeUserAccessCommandHandler(
    IdpDbContext db,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    ClientDataService.ClientDataServiceClient clientDataClient,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<ChangeUserAccessCommand, UserAccessResponse>
{
    public async Task<UserAccessResponse> ExecuteAsync(ChangeUserAccessCommand command, CancellationToken ct)
    {
        var poolId = await PoolApiCaller.RequirePoolAsync(requestContextAccessor, db, authorizationClient, AuthConstants.ManagerRelation, ct);

        var roleIds = (command.RoleIds ?? []).Distinct().ToList();
        var scopes = (command.Scopes ?? []).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Distinct().ToList();

        var definitions = await clientDataClient.GetPoolDefinitionsAsync(
            new GetPoolDefinitionsRequest { UserPoolId = poolId.ToString() }, cancellationToken: ct);

        if (!definitions.Found)
            throw new BusinessException(IdpMessageKeys.PoolNotFound);

        var poolRoles = definitions.Roles.ToDictionary(r => Guid.Parse(r.Id), r => r.Key);

        // Kaldirmada tanimi silinmis bir kaydin temizlenmesine izin verilir; eklemede yalnizca tanimli olanlar.
        if (command.Grant)
        {
            if (roleIds.Any(id => !poolRoles.ContainsKey(id)))
                throw new BusinessException(IdpMessageKeys.RoleNotInPool);

            if (scopes.Any(s => !definitions.Scopes.Contains(s)))
                throw new BusinessException(IdpMessageKeys.ScopeNotInPool);
        }

        using var _ = requestContextAccessor.BeginSystemContext(poolId);

        await UserAccessRules.EnsureUserAsync(db, command.PrincipalId, ct);

        var currentRoles = await principalRoleRepository.Query().Where(r => r.PrincipalId == command.PrincipalId).ToListAsync(ct);
        var currentScopes = await principalScopeRepository.Query().Where(s => s.PrincipalId == command.PrincipalId).ToListAsync(ct);

        if (command.Grant)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var id in roleIds.Where(id => currentRoles.All(r => r.RoleId != id)))
                await principalRoleRepository.AddAsync(new PrincipalRole
                {
                    Id = EntityId.New(), PrincipalId = command.PrincipalId, RoleId = id, RoleName = poolRoles[id], CreatedTime = now
                }, ct);

            foreach (var scope in scopes.Where(s => currentScopes.All(c => c.Scope != s)))
                await principalScopeRepository.AddAsync(new PrincipalScope
                {
                    Id = EntityId.New(), PrincipalId = command.PrincipalId, Scope = scope, CreatedTime = now
                }, ct);
        }
        else
        {
            foreach (var role in currentRoles.Where(r => roleIds.Contains(r.RoleId)))
                principalRoleRepository.Delete(role, softDelete: false);

            foreach (var scope in currentScopes.Where(s => scopes.Contains(s.Scope)))
                principalScopeRepository.Delete(scope, softDelete: false);
        }

        await principalRoleRepository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(command.PrincipalId), ct);

        return await UserAccessRules.ReadAsync(db, command.PrincipalId, ct);
    }
}

public class ChangeUserAccessValidator : Validator<ChangeUserAccessRequest>
{
    public ChangeUserAccessValidator()
    {
        RuleFor(x => x).Must(x => (x.RoleIds?.Count ?? 0) + (x.Scopes?.Count ?? 0) > 0)
            .WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.RoleIds!.Count).LessThanOrEqualTo(100).When(x => x.RoleIds is not null)
            .WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Scopes!.Count).LessThanOrEqualTo(100).When(x => x.Scopes is not null)
            .WithMessage(ValidationErrors.MaxLengthError);
    }
}

// ----------------------------------------------------------------------------- ortak

internal static class UserAccessRules
{
    /// <summary>Cagiran BeginSystemContext(havuz) icinde olmali; izolasyon filtresi baska havuzun kullanicisini gizler.</summary>
    public static async Task EnsureUserAsync(IdpDbContext db, Guid principalId, CancellationToken ct)
    {
        var exists = await db.Principals.AnyAsync(p => p.Id == principalId && p.Type == PrincipalType.User, ct);
        if (!exists)
            throw new BusinessException(IdpMessageKeys.PrincipalNotFound);
    }

    public static async Task<UserAccessResponse> ReadAsync(IdpDbContext db, Guid principalId, CancellationToken ct)
    {
        var roles = await db.PrincipalRoles.Where(r => r.PrincipalId == principalId)
            .OrderBy(r => r.RoleName).Select(r => new UserAccessRole(r.RoleId, r.RoleName)).ToListAsync(ct);

        var scopes = await db.PrincipalScopes.Where(s => s.PrincipalId == principalId)
            .OrderBy(s => s.Scope).Select(s => s.Scope).ToListAsync(ct);

        return new UserAccessResponse(roles, scopes);
    }
}
