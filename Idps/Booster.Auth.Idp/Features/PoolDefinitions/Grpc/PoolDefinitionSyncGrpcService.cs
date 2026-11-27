using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.PoolDefinitions.Grpc;

/// <summary>
/// Management.Api'de adi degisen ya da silinen rol / kapsami kullanicilara kopyalanmis kayitlarda uygular
/// (token bu kopyalardan uretilir). Management cagriyi beklemez; IdP kapaliysa kopya bayat kalir.
/// </summary>
[AllowAnonymous]
public sealed class PoolDefinitionSyncGrpcService(IdpDbContext db, IRequestContextAccessor requestContextAccessor)
    : PoolDefinitionSyncService.PoolDefinitionSyncServiceBase
{
    public override async Task<PoolDefinitionSyncAck> RoleChanged(RoleChangedRequest request, ServerCallContext context)
    {
        var poolId = ParseGuid(request.UserPoolId, "user_pool_id");
        var roleId = ParseGuid(request.RoleId, "role_id");
        var ct = context.CancellationToken;

        using var _ = requestContextAccessor.BeginSystemContext(poolId);

        var rows = db.PrincipalRoles.Where(r => r.RoleId == roleId);

        if (string.IsNullOrEmpty(request.NewKey))
        {
            await rows.ExecuteDeleteAsync(ct);
            return new PoolDefinitionSyncAck();
        }

        await rows.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RoleName, request.NewKey)
            .SetProperty(x => x.UpdatedTime, (DateTimeOffset?)DateTimeOffset.UtcNow), ct);

        return new PoolDefinitionSyncAck();
    }

    public override async Task<PoolDefinitionSyncAck> ScopeDefinitionChanged(
        ScopeDefinitionChangedRequest request, ServerCallContext context)
    {
        var poolId = ParseGuid(request.UserPoolId, "user_pool_id");
        if (string.IsNullOrWhiteSpace(request.OldScope))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "old_scope bos."));

        var ct = context.CancellationToken;
        var oldScope = request.OldScope;

        using var _ = requestContextAccessor.BeginSystemContext(poolId);

        var oldRows = db.PrincipalScopes.Where(s => s.Scope == oldScope);

        if (string.IsNullOrEmpty(request.NewScope))
        {
            await oldRows.ExecuteDeleteAsync(ct);
            return new PoolDefinitionSyncAck();
        }

        var newScope = request.NewScope;

        // Yeni adı zaten taşıyan kullanıcıda eski satır silinir; yoksa (PrincipalId, Scope) benzersiz indeksi bozulurdu.
        await oldRows
            .Where(s => db.PrincipalScopes.Any(x => x.PrincipalId == s.PrincipalId && x.Scope == newScope))
            .ExecuteDeleteAsync(ct);

        await oldRows.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Scope, newScope)
            .SetProperty(x => x.UpdatedTime, (DateTimeOffset?)DateTimeOffset.UtcNow), ct);

        return new PoolDefinitionSyncAck();
    }

    private static Guid ParseGuid(string value, string name)
        => Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"{name} gecersiz."));
}
