using Booster.Auth.Database.Models;
using Booster.Caching.Abstraction;

namespace Booster.Auth.Management.Api.Stores.ClientDataStores;

/// <inheritdoc cref="IClientDataCacheStore"/>
public sealed class ClientDataCacheStore(ICacheService cacheService) : IClientDataCacheStore
{
    public Task InvalidateClientAsync(string clientKey, CancellationToken ct)
    {

        if (string.IsNullOrWhiteSpace(clientKey))
            return Task.CompletedTask;

        return cacheService.RemoveByTagAsync(
            ClientDataCache.Tag(clientKey.Trim().ToLowerInvariant()), ct);
    }

    public Task InvalidatePoolAsync(Guid userPoolId, CancellationToken ct)
        => cacheService.RemoveByTagAsync(ClientDataCache.PoolTag(userPoolId), ct);
}
