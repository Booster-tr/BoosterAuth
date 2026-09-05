using Booster.Auth.Idp.Stores.ClientStores.Models;

namespace Booster.Auth.Idp.Stores.ClientStores;

public interface IClientDataStore
{
    Task<ClientData?> GetByClientKeyAsync(string clientKey, CancellationToken ct);
}
