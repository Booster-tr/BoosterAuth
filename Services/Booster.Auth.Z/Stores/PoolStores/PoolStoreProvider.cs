using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores.Models;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Z.Stores.PoolStores;

/// <summary>Havuzun store'u ve yetki kontrollerinde kullanılan model: aktif şema sürümünden okunur.</summary>
public sealed class PoolStoreProvider(
    IAsyncRepository<PoolSchema, AuthZDbContext> repository,
    ICacheService cacheService) : IPoolStoreProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private static string CacheKey(Guid poolId) => $"authz:poolstore:{poolId}";

    public static string Tag(Guid poolId) => $"poolstore:{poolId}";

    public async Task<PoolStoreData?> GetByPoolIdAsync(Guid poolId, CancellationToken ct)
        => await cacheService.GetOrSetAsync<PoolStoreData?>(
            CacheKey(poolId),
            async (context, token) =>
            {
                context.SetTags(Tag(poolId));

                // Şema tanımı (JSON) okunmaz; yetki kontrolü yalnızca store ve modeli ister.
                return await repository.Query()
                    .AsNoTracking()
                    .Where(x => x.PoolId == poolId && x.Status == SchemaVersionStatus.Active && x.ModelId != null)
                    .Select(x => new PoolStoreData(x.PoolId, x.StoreId, x.ModelId!))
                    .FirstOrDefaultAsync(token);
            },
            expiry: Ttl,
            ct: ct);

    public Task RemoveCacheAsync(Guid poolId, CancellationToken ct)
        => cacheService.RemoveByTagAsync(Tag(poolId), ct);
}
