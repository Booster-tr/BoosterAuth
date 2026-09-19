using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores.Models;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;

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
                var entity = await repository.GetAsync(
                    predicate: x => x.PoolId == poolId,
                    enableTracking: false,
                    cancellationToken: token);

                if (entity is null)
                    return null;

                context.SetTags(Tag(poolId));

                return ToData(entity);
            },
            expiry: Ttl,
            ct: ct);

    public async Task<PoolStoreData> UpsertAsync(
        Guid poolId, string storeId, string activeModelId, CancellationToken ct)
    {
        var entity = await repository.GetAsync(x => x.PoolId == poolId, cancellationToken: ct);

        if (entity is null)
        {
            entity = new PoolStore
            {
                Id = EntityId.New(),
                PoolId = poolId,
                StoreId = storeId,
                ActiveModelId = activeModelId,
                CreatedTime = DateTimeOffset.UtcNow
            };

            await repository.AddAsync(entity, ct);
        }
        else
        {
            entity.StoreId = storeId;
            entity.ActiveModelId = activeModelId;
            entity.UpdatedTime = DateTimeOffset.UtcNow;
        }

        await repository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(Tag(poolId), ct);

        return ToData(entity);
    }

    public async Task UpdateActiveModelAsync(Guid poolId, string activeModelId, CancellationToken ct)
    {
        var entity = await repository.GetAsync(x => x.PoolId == poolId, cancellationToken: ct)
            ?? throw new BusinessException(AuthZMessageKeys.PoolStoreNotFound);

        if (entity.ActiveModelId == activeModelId)
            return;

        entity.ActiveModelId = activeModelId;
        entity.UpdatedTime = DateTimeOffset.UtcNow;

        await repository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(Tag(poolId), ct);
    }

    private static PoolStoreData ToData(PoolStore entity)
        => new(entity.PoolId, entity.StoreId, entity.ActiveModelId);
}
