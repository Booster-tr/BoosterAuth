namespace Booster.Auth.Management.Api.Stores.ClientDataStores;

public interface IClientDataCacheStore
{
    Task InvalidateClientAsync(string clientKey, CancellationToken ct);

    Task InvalidatePoolAsync(Guid userPoolId, CancellationToken ct);
}
