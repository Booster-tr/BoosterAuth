using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;

namespace Booster.Auth.Z.Bootstrappers;

/// <summary>
<<<<<<< Updated upstream:Services/Booster.Auth.Z/Bootstrappers/LicensingBootstrapper.cs
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
=======
/// WalkToWin'in AuthZ kurulumu (Management.Api tarafındaki WalkToWinBootstrapper'ın karşılığı):
/// <list type="number">
/// <item>Yönetim store'unda: <c>user:admin owner tenant:wtw</c>, <c>tenant:wtw parent pool:wtw</c>,
/// <c>pool:wtw parent client:wtw</c> — sistem yöneticisi WalkToWin'i panelde görür.</item>
/// <item>WalkToWin havuzunun KENDİ store'u ve şeması (<see cref="WalkToWinSchema"/>). Oyuncu token'larının
/// <c>actor</c>'ı bu havuzdur; FGA kontrolleri bu store'a gider. Şema olmasaydı WalkToWin API'sindeki her
/// FGA kontrolü FailedPrecondition ile 500 dönerdi.</item>
/// </list>
/// İkisi de idempotent: havuzun aktif şeması zaten varsa (panelden düzenlenmiş olabilir) üzerine yazılmaz.
>>>>>>> Stashed changes:Services/Booster.Auth.Z/Bootstrappers/WalkToWinBootstrapper.cs
/// </summary>
public sealed class LicensingBootstrapper(
    FgaRelationManager relationManager,
    FgaSchemaWriter schemaWriter,
    IPoolStoreProvider poolStoreProvider,
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    ILogger<WalkToWinBootstrapper> logger)
{
    private const string SchemaName = "WalkToWin";

    public async Task InitializeAsync(CancellationToken ct)
    {
        await LinkToManagementGraphAsync(ct);
        await EnsurePoolSchemaAsync(ct);
    }

    private async Task LinkToManagementGraphAsync(CancellationToken ct)
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

    private async Task EnsurePoolSchemaAsync(CancellationToken ct)
    {
        var poolId = AuthConstants.WalkToWinPoolId;

        var active = await schemaRepository.GetAsync(
            s => s.PoolId == poolId && s.Status == SchemaVersionStatus.Active,
            enableTracking: false, cancellationToken: ct);

        if (active is not null)
        {
            // Panelden kurulmuş ama WalkToWin tiplerini içermeyen bir şema varsa API'nin FGA kontrolleri
            // hep "izin yok" döner; üzerine yazmak panel sürümünü silerdi. Bu yüzden yalnızca uyarılır.
            var types = active.Definition.Types.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
            if (!types.Contains(AuthConstants.WalkToWinWorkspaceType) || !types.Contains(AuthConstants.WalkToWinHuntType))
                logger.LogWarning("WalkToWin havuzunun aktif şeması workspace/hunt tiplerini içermiyor; WalkToWin yönetim yetkileri çalışmaz.");
            return;
        }

        var definition = WalkToWinSchema.Definition;
        var storeId = await schemaWriter.CreateStoreAsync($"pool-{poolId}", ct);
        var modelId = await schemaWriter.WriteModelAsync(storeId, definition.ToFgaModel(), ct);

        var schema = new PoolSchema
        {
            PoolId = poolId,
            StoreId = storeId,
            Name = SchemaName,
            Status = SchemaVersionStatus.Active,
            Definition = definition,
            ModelId = modelId,
            Revision = 1,
            ActivatedTime = DateTimeOffset.UtcNow
        };
        schema.SetId();

        await schemaRepository.AddAsync(schema, ct);
        await schemaRepository.SaveChangesAsync(ct);

        await poolStoreProvider.RemoveCacheAsync(poolId, ct);
    }
}

/// <summary>
/// WalkToWin havuzunun başlangıç yetki şeması (walk-to-win/docs/security.md).
/// <code>
/// workspace: admin [user], creator [user] + admin
/// hunt:      parent [workspace], owner [user] + parent.admin, manager [user] + owner, viewer [user] + manager
/// </code>
/// Admin'in her ava ayrı tuple yazılmadan erişmesi parent mirasıyla sağlanır.
/// </summary>
public static class WalkToWinSchema
{
    private const string User = AuthConstants.UserPrefix;
    private const string Parent = AuthConstants.ParentRelation;

    public static SchemaDefinition Definition => new()
    {
        Types =
        [
            new SchemaType
            {
                Name = AuthConstants.WalkToWinWorkspaceType,
                DisplayName = "Çalışma alanı",
                X = 40,
                Y = 40,
                Relations =
                [
                    Relation(AuthConstants.WalkToWinAdminRelation, "Yönetici"),
                    Relation(AuthConstants.WalkToWinCreatorRelation, "Av oluşturucu", includes: [AuthConstants.WalkToWinAdminRelation])
                ]
            },
            new SchemaType
            {
                Name = AuthConstants.WalkToWinHuntType,
                DisplayName = "Av",
                X = 420,
                Y = 40,
                Relations =
                [
                    new SchemaRelation { Name = Parent, DisplayName = "Çalışma alanı", AssignableTypes = [AuthConstants.WalkToWinWorkspaceType] },
                    Relation(AuthConstants.OwnerRelation, "Sahip",
                        inherits: [new SchemaInherit { Via = Parent, Relation = AuthConstants.WalkToWinAdminRelation }]),
                    Relation(AuthConstants.ManagerRelation, "Yönetici", includes: [AuthConstants.OwnerRelation]),
                    Relation(AuthConstants.ViewerRelation, "Görüntüleyici", includes: [AuthConstants.ManagerRelation])
                ]
            }
        ]
    };

    private static SchemaRelation Relation(string name, string displayName, string[]? includes = null, SchemaInherit[]? inherits = null)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            AssignableTypes = [User],
            Includes = [.. includes ?? []],
            Inherits = [.. inherits ?? []]
        };
}
