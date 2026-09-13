using OpenFga.Sdk.Client;

namespace Booster.Authorization.Relational.Fga;

public interface IFgaClientProvider
{
    OpenFgaClient GetClient();

    OpenFgaClient GetClientForStore(string storeId, string? authorizationModelId = null);
}
