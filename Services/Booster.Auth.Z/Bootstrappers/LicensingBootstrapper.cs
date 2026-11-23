using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;

namespace Booster.Auth.Z.Bootstrappers;

/// <summary>
/// Lisans portali kaynaklarini yonetim yetki grafigine baglar (Management.Api tarafindaki
/// LicensingBootstrapper'ın AuthZ karşılığı):
/// <code>
/// user:admin      owner  tenant:booster-licensing
/// tenant:licensing parent pool:booster-musterileri
/// pool:licensing   parent client:licensing-portal
/// </code>
/// Boylece sistem yoneticisi yonetim panelinde musteri havuzunu ve portal client'ini gorur.
/// Program.cs yalnizca BootstrapOptions:Licensing:Enabled true ise cagirir.
/// İlişkiler zaten varsa hiçbir şey yazmaz (idempotent).
/// </summary>
public sealed class LicensingBootstrapper(
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
                AuthConstants.Tenant(AuthConstants.LicensingTenantId)),

            new TupleInput(
                AuthConstants.Tenant(AuthConstants.LicensingTenantId),
                AuthConstants.ParentRelation,
                AuthConstants.Pool(AuthConstants.LicensingPoolId)),

            new TupleInput(
                AuthConstants.Pool(AuthConstants.LicensingPoolId),
                AuthConstants.ParentRelation,
                AuthConstants.Client(AuthConstants.LicensingClientId))
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
