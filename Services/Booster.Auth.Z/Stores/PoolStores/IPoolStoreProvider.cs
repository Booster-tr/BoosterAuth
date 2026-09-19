using Booster.Auth.Z.Stores.PoolStores.Models;

namespace Booster.Auth.Z.Stores.PoolStores;

public interface IPoolStoreProvider
{
    Task<PoolStoreData?> GetByPoolIdAsync(Guid poolId, CancellationToken ct);

    Task<PoolStoreData> UpsertAsync(Guid poolId, string storeId, string activeModelId, CancellationToken ct);

    Task UpdateActiveModelAsync(Guid poolId, string activeModelId, CancellationToken ct);
}
