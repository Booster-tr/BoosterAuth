using Booster.Auth.Management.Grpc;

namespace Booster.Auth.Idp.Stores.FederationStores;

public sealed class FederationProviderConfigStore(
    ClientDataService.ClientDataServiceClient clientDataClient) : IFederationProviderConfigStore
{
    public async Task<FederationProviderConfig?> GetAsync(Guid userPoolId, string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var response = await clientDataClient.GetFederationProviderAsync(
            new GetFederationProviderRequest
            {
                UserPoolId = userPoolId.ToString(),
                Key = key
            },
            cancellationToken: ct);

        if (!response.Found || response.Provider is null)
            return null;

        var provider = response.Provider;

        return new FederationProviderConfig
        {
            Id = Guid.Parse(provider.Id),
            Key = provider.Key,
            ProviderType = provider.ProviderType,
            DisplayName = provider.DisplayName,
            IsActive = provider.IsActive,
            Settings = provider.Settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal)
        };
    }
}
