using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Stores.PoolStores.Models;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Z.Stores.PoolStores;

public static class PoolStoreProviderExtensions
{
    public static async Task<PoolStoreData> GetRequiredByPoolIdAsync(
        this IPoolStoreProvider provider, Guid poolId, CancellationToken ct)
        => await provider.GetByPoolIdAsync(poolId, ct)
           ?? throw new BusinessException(AuthZMessageKeys.PoolStoreNotFound);
}
