using Booster.Authorization.Relational.Options;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

namespace Booster.Authorization.Relational.Fga;

public sealed class FgaClientProvider(FgaOptions options) : IFgaClientProvider
{
    private readonly OpenFgaClient _baseClient = new(BuildConfig(options, storeId: null, modelId: null));

    public OpenFgaClient GetClient() => _baseClient;

    public OpenFgaClient GetClientForStore(string storeId, string? authorizationModelId = null)
        => new(BuildConfig(options, storeId, authorizationModelId));

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
}
