using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Principals.Grpc;

/// <summary>
/// Management'taki rol/kapsam değişikliklerini IdP'ye yansıtır. Atamaların yanında
/// aktif refresh token'lar ve onaylar da güncellenir; yoksa token yenilendikçe
/// silinmiş ya da eski adlı rol/kapsam taşınmaya devam ederdi.
/// </summary>
[AllowAnonymous]
public sealed class PrincipalAccessGrpcService(
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    IAsyncRepository<RefreshToken, IdpDbContext> refreshTokenRepository,
    IAsyncRepository<Consent, IdpDbContext> consentRepository,
    IRequestContextAccessor requestContextAccessor)
    : PrincipalAccessService.PrincipalAccessServiceBase
{
    public override async Task<PrincipalAccessSyncResponse> RenameRole(RenameRoleRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var roleId = ParseGuid(request.RoleId, "role_id");
        var (oldKey, newKey) = (Required(request.OldKey, "old_key"), Required(request.NewKey, "new_key"));

        using var _ = requestContextAccessor.BeginSystemContext(ParseGuid(request.IsolationId, "isolation_id"));

        var roles = await principalRoleRepository.Query().Where(r => r.RoleId == roleId).ToListAsync(ct);

        foreach (var role in roles)
            role.RoleName = newKey;

        await RewriteRefreshTokensAsync(TokenList.Roles, oldKey, newKey, ct);
        await principalRoleRepository.SaveChangesAsync(ct);

        return Affected(roles.Select(r => r.PrincipalId));
    }

    public override async Task<PrincipalAccessSyncResponse> RemoveRole(RemoveRoleRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var roleId = ParseGuid(request.RoleId, "role_id");
        var key = Required(request.Key, "key");

        using var _ = requestContextAccessor.BeginSystemContext(ParseGuid(request.IsolationId, "isolation_id"));

        var roles = await principalRoleRepository.Query().Where(r => r.RoleId == roleId).ToListAsync(ct);

        foreach (var role in roles)
            principalRoleRepository.Delete(role, softDelete: false);

        await RewriteRefreshTokensAsync(TokenList.Roles, key, null, ct);
        await principalRoleRepository.SaveChangesAsync(ct);

        return Affected(roles.Select(r => r.PrincipalId));
    }

    public override async Task<PrincipalAccessSyncResponse> RenameScope(RenameScopeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var (oldScope, newScope) = (Required(request.OldScope, "old_scope"), Required(request.NewScope, "new_scope"));

        using var _ = requestContextAccessor.BeginSystemContext(ParseGuid(request.IsolationId, "isolation_id"));

        var scopes = await principalScopeRepository.Query().Where(s => s.Scope == oldScope).ToListAsync(ct);

        // Kimlik yeni adı zaten taşıyorsa eski satır kopya olur; silinir.
        var principalIds = scopes.Select(s => s.PrincipalId).ToList();
        var alreadyHasNew = await principalScopeRepository.Query()
            .Where(s => s.Scope == newScope && principalIds.Contains(s.PrincipalId))
            .Select(s => s.PrincipalId)
            .ToListAsync(ct);

        foreach (var scope in scopes)
        {
            if (alreadyHasNew.Contains(scope.PrincipalId))
                principalScopeRepository.Delete(scope, softDelete: false);
            else
                scope.Scope = newScope;
        }

        await RewriteRefreshTokensAsync(TokenList.Scopes, oldScope, newScope, ct);
        await RewriteConsentsAsync(oldScope, newScope, ct);
        await principalScopeRepository.SaveChangesAsync(ct);

        return Affected(principalIds);
    }

    public override async Task<PrincipalAccessSyncResponse> RemoveScope(RemoveScopeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var scopeName = Required(request.Scope, "scope");

        using var _ = requestContextAccessor.BeginSystemContext(ParseGuid(request.IsolationId, "isolation_id"));

        var scopes = await principalScopeRepository.Query().Where(s => s.Scope == scopeName).ToListAsync(ct);

        foreach (var scope in scopes)
            principalScopeRepository.Delete(scope, softDelete: false);

        await RewriteRefreshTokensAsync(TokenList.Scopes, scopeName, null, ct);
        await RewriteConsentsAsync(scopeName, null, ct);
        await principalScopeRepository.SaveChangesAsync(ct);

        return Affected(scopes.Select(s => s.PrincipalId));
    }

    private async Task RewriteRefreshTokensAsync(TokenList list, string from, string? to, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var query = refreshTokenRepository.Query().Where(t => t.RevokedAt == null && t.ExpiresAt > now);

        // LIKE ön elemesi; kesin eşleşme boşlukla ayrılmış liste üzerinde yapılır.
        query = list == TokenList.Roles
            ? query.Where(t => t.GrantedRoles.Contains(from))
            : query.Where(t => t.GrantedScopes.Contains(from));

        foreach (var token in await query.ToListAsync(ct))
        {
            if (list == TokenList.Roles)
                token.GrantedRoles = Rewrite(token.GrantedRoles, from, to);
            else
                token.GrantedScopes = Rewrite(token.GrantedScopes, from, to);
        }
    }

    private async Task RewriteConsentsAsync(string from, string? to, CancellationToken ct)
    {
        var consents = await consentRepository.Query()
            .Where(c => c.RevokedAt == null && c.GrantedScopes.Contains(from))
            .ToListAsync(ct);

        foreach (var consent in consents)
            consent.GrantedScopes = Rewrite(consent.GrantedScopes, from, to);
    }

    /// <summary>Boşlukla ayrılmış listede <paramref name="from"/>'u <paramref name="to"/> ile değiştirir; to null ise çıkarır.</summary>
    private static string Rewrite(string list, string from, string? to)
    {
        var items = list.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!items.Contains(from))
            return list;

        items = items
            .Select(item => item == from ? to : item)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return string.Join(' ', items);
    }

    private static PrincipalAccessSyncResponse Affected(IEnumerable<Guid> principalIds)
        => new() { AffectedPrincipals = principalIds.Distinct().Count() };

    private static Guid ParseGuid(string value, string name)
        => Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"{name} geçersiz."));

    private static string Required(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new RpcException(new Status(StatusCode.InvalidArgument, $"{name} boş olamaz."))
            : value;

    private enum TokenList { Roles, Scopes }
}
