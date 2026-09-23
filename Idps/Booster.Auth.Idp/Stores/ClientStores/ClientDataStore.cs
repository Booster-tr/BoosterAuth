using Booster.Auth.Database.Models;
using Booster.Auth.Management.Grpc;
using Booster.Caching.Abstraction;

namespace Booster.Auth.Idp.Stores.ClientStores;

public sealed class ClientDataStore(
    ClientDataService.ClientDataServiceClient clientDataClient,
    ICacheService cacheService) : IClientDataStore
{

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public async Task<ClientData?> GetByClientKeyAsync(string clientKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientKey))
            return null;

        return await cacheService.GetOrSetAsync<ClientData?>(
            ClientDataCache.Key(clientKey),
            async (context, token) =>
            {
                var response = await clientDataClient.GetByClientKeyAsync(
                    new GetClientDataRequest { ClientKey = clientKey },
                    cancellationToken: token);

                if (!response.Found)
                {
                    context.SetTags(ClientDataCache.Tag(clientKey));
                    return null;
                }

                var clientData = ClientDataMapper.ToModel(response.Client);

                context.SetTags(
                    ClientDataCache.Tag(clientKey),
                    ClientDataCache.PoolTag(clientData.IsolationId));

                return clientData;
            },
            expiry: Ttl,
            ct: ct);
    }
}
