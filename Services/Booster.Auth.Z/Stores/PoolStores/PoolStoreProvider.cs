using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores.Models;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;

namespace Booster.Auth.Z.Stores.PoolStores;

public sealed class PoolStoreProvider(
    IAsyncRepository<PoolStore, AuthZDbContext> repository,
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

                var entity = await repository.GetAsync(
                    predicate: x => x.PoolId == poolId,
                    enableTracking: false,
                    cancellationToken: token);

                if (entity is null)
                    return null;

                return new PoolStoreData(entity.PoolId, entity.StoreId, entity.ActiveModelId);
            },
            expiry: Ttl,
            ct: ct);

    public Task RemoveCacheAsync(Guid poolId, CancellationToken ct)
        => cacheService.RemoveByTagAsync(Tag(poolId), ct);
}
