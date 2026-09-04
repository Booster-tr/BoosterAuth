using Booster.Auth.Idp.Stores.ClientStore.Models;

namespace Booster.Auth.Idp.Stores.ClientStore;

public interface IClientDataStore
{
    Task<ClientData?> GetByClientKeyAsync(string clientKey, CancellationToken ct);
}
