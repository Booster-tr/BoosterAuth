using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;

namespace Booster.Auth.Z.Bootstrappers;

/// <summary>
/// Walk to Win kaynaklarını yönetim yetki grafiğine bağlar (Management.Api tarafındaki
/// WalkToWinBootstrapper'ın AuthZ karşılığı):
/// <code>
/// user:admin      owner  tenant:walk-to-win
/// tenant:wtw      parent pool:walk-to-win-players
/// pool:wtw        parent client:walk-to-win
/// </code>
/// Böylece sistem yöneticisi yönetim panelinde Walk to Win tenant'ını, havuzunu ve client'ını görür.
/// İlişkiler zaten varsa hiçbir şey yazmaz (idempotent).
/// </summary>
public sealed class WalkToWinBootstrapper(
    FgaRelationManager relationManager,
    IPoolStoreProvider poolStoreProvider)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var store = await poolStoreProvider.GetByPoolIdAsync(AuthConstants.ManagementPoolId, ct);

        // Yönetim store'u ManagementBootstrapper tarafından oluşturulur; yoksa yapılacak bir şey yok.
        if (store is null)
            return;

        var tuples = new[]
        {
            new TupleInput(
                AuthConstants.User(AuthConstants.AdminUserId),
                AuthConstants.OwnerRelation,
                AuthConstants.Tenant(AuthConstants.WalkToWinTenantId)),

            new TupleInput(
                AuthConstants.Tenant(AuthConstants.WalkToWinTenantId),
                AuthConstants.ParentRelation,
                AuthConstants.Pool(AuthConstants.WalkToWinPoolId)),

            new TupleInput(
                AuthConstants.Pool(AuthConstants.WalkToWinPoolId),
                AuthConstants.ParentRelation,
                AuthConstants.Client(AuthConstants.WalkToWinClientId))
        };

        // OpenFGA mevcut bir tuple'ı yeniden yazmayı reddeder; yalnızca eksikler yazılır.
        var missing = new List<TupleInput>();
        foreach (var tuple in tuples)
        {
            var existing = await relationManager.ReadAsync(store.StoreId, tuple.User, tuple.Relation, tuple.Object, ct);
            if (existing.Count == 0)
                missing.Add(tuple);
        }

        if (missing.Count > 0)
            await relationManager.WriteBatchAsync(store.StoreId, missing, ct);
    }
}
