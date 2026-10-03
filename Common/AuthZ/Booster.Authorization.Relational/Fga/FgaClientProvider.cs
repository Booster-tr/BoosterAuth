using System.Collections.Concurrent;
using Booster.Authorization.Relational.Options;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

namespace Booster.Authorization.Relational.Fga;

/// <summary>
/// İstemciler store+model başına bir kez kurulur ve tek bir bağlantı havuzlu HttpClient'ı paylaşır.
/// Her çağrıda yeni istemci kurmak her seferinde yeni HttpClient/soket açıyordu.
/// Modeller değişmez olduğu için önbellekteki istemci hiçbir zaman bayatlamaz.
/// </summary>
public sealed class FgaClientProvider(FgaOptions options) : IFgaClientProvider, IDisposable
{
    private readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        EnableMultipleHttp2Connections = true
    });

    private readonly ConcurrentDictionary<(string? StoreId, string? ModelId), OpenFgaClient> _clients = new();

    public OpenFgaClient GetClient() => Get(null, null);

    public OpenFgaClient GetClientForStore(string storeId, string? authorizationModelId = null)
        => Get(storeId, string.IsNullOrEmpty(authorizationModelId) ? null : authorizationModelId);

    private OpenFgaClient Get(string? storeId, string? modelId)
        => _clients.GetOrAdd(
            (storeId, modelId),
            key => new OpenFgaClient(BuildConfig(options, key.StoreId, key.ModelId), _httpClient));

    private static ClientConfiguration BuildConfig(FgaOptions options, string? storeId, string? modelId)
    {
        var config = new ClientConfiguration
        {
            ApiUrl = options.ApiUrl,
            StoreId = storeId,
            AuthorizationModelId = modelId
        };

        if (!string.IsNullOrEmpty(options.ApiToken))
        {
            config.Credentials = new Credentials
            {
                Method = CredentialsMethod.ApiToken,
                Config = new CredentialsConfig { ApiToken = options.ApiToken }
            };
        }

        return config;
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
            client.Dispose();

        _httpClient.Dispose();
    }
}
