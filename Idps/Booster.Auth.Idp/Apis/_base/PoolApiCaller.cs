using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Apis._base;

/// <summary>
/// /apis uclarini cagiran: panelde bir havuz icin acilmis SERVIS HESABI (yonetim havuzundaki Machine principal,
/// client_credentials token'i). Islem yapilan havuz hesabin <c>OwnerPoolId</c>'sidir; istekte havuz ALINMAZ.
/// Panelde verilen iliski aynen uygulanir (yonetim grafigi: user:&lt;hesap&gt; &lt;iliski&gt; pool:&lt;havuz&gt;);
/// panel uclarindan farki booster:panel kapsami istememesidir.
/// </summary>
public static class PoolApiCaller
{
    public static async Task<Guid> RequirePoolAsync(
        IRequestContextAccessor accessor,
        IdpDbContext db,
        AuthorizationService.AuthorizationServiceClient authorizationClient,
        string relation,
        CancellationToken ct)
    {
        var context = accessor.Context();

        var isMachine = string.Equals(context.PrincipalType, PrincipalType.Machine.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!isMachine || context.Isolation != AuthConstants.ManagementPoolId || context.PrincipalId == Guid.Empty)
            throw new ForbiddenException(IdpMessageKeys.PoolApiCallerNotAllowed);

        Guid? poolId;
        using (accessor.BeginSystemContext(AuthConstants.ManagementPoolId))
        {
            var now = DateTimeOffset.UtcNow;
            poolId = await db.ServiceAccounts
                .Where(s => s.PrincipalId == context.PrincipalId && s.Status == ServiceAccountStatus.Active
                            && (s.ExpiresAt == null || s.ExpiresAt > now))
                .Select(s => s.OwnerPoolId)
                .FirstOrDefaultAsync(ct);
        }

        if (poolId is null || poolId == AuthConstants.ManagementPoolId)
            throw new ForbiddenException(IdpMessageKeys.PoolApiCallerNotAllowed);

        var check = await authorizationClient.CheckAsync(new CheckRequest
        {
            IsolationId = AuthConstants.ManagementPoolId.ToString(),
            User = AuthConstants.User(context.PrincipalId),
            Relation = relation,
            Object = AuthConstants.Pool(poolId.Value)
        }, cancellationToken: ct);

        // Cagiran dogru (servis hesabi) ama bu islem icin yetkisi yetmiyor: farkli mesaj, yoksa entegratör
        // "yalnizca servis hesabi cagirabilir" gorup yanlis yerde ariyordu.
        if (!check.Allowed)
            throw new ForbiddenException(IdpMessageKeys.PoolApiRelationTooLow);

        return poolId.Value;
    }
}

public static class PoolApiRoutes
{
    public const string Users = "apis/users";
    public const string Relations = "apis/relations";
}
