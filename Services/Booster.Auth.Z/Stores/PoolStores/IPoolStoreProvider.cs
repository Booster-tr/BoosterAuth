using Booster.Auth.Z.Stores.PoolStores.Models;

namespace Booster.Auth.Z.Stores.PoolStores;

public interface IPoolStoreProvider
{
    Task<PoolStoreData?> GetByPoolIdAsync(Guid poolId, CancellationToken ct);

    Task RemoveCacheAsync(Guid poolId, CancellationToken ct);
}
