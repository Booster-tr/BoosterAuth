using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Z.Managers.QueryAccess;

/// <summary>
/// Sorgu uclari (<c>check</c>, <c>batch-check</c>, <c>lookup/*</c>) hangi havuzun deposuna bakilacagini istek
/// govdesinden (<c>isolationId</c>) alir. Kural:
/// <list type="bullet">
/// <item>Token'i o havuz verdiyse (<c>actor</c> = isolationId) serbest: musteri uygulamasi kendi havuzunu sorgular.</item>
/// <item>Yonetim havuzunun token'i (panel kullanicisi ya da havuz servis hesabi) ise yonetim deposunda o havuz
/// uzerinde <c>viewer</c> olmalidir.</item>
/// </list>
/// Aksi halde herhangi bir havuzun token'i baska havuzun (yonetim dahil) iliskilerini okuyabiliyordu.
/// </summary>
public sealed class QueryCallerGuard(
    IRequestContextAccessor requestContextAccessor,
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
{
    public async Task EnsureAsync(Guid isolationId, CancellationToken ct)
    {
        var context = requestContextAccessor.Context();

        if (context.Isolation != Guid.Empty && context.Isolation == isolationId)
            return;

        if (context.Isolation == AuthConstants.ManagementPoolId && context.PrincipalId != Guid.Empty)
        {
            var management = await poolStoreProvider.GetRequiredByPoolIdAsync(AuthConstants.ManagementPoolId, ct);
            var allowed = await relationManager.CheckAsync(
                management.StoreId,
                AuthConstants.User(context.PrincipalId),
                AuthConstants.ViewerRelation,
                AuthConstants.Pool(isolationId),
                management.ActiveModelId,
                ct);

            if (allowed)
                return;
        }

        throw new ForbiddenException(AuthZMessageKeys.QueryNotAllowed);
    }
}
