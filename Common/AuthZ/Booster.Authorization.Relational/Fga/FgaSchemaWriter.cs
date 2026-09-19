using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;

namespace Booster.Authorization.Relational.Fga;

public sealed class FgaSchemaWriter(IFgaClientProvider clientProvider)
{
    public async Task<string> CreateStoreAsync(string name, CancellationToken ct = default)
    {
        var client = clientProvider.GetClient();
        var response = await client.CreateStore(new ClientCreateStoreRequest { Name = name }, cancellationToken: ct);
        return response.Id;
    }

    public async Task<string> WriteModelAsync(
        string storeId, ClientWriteAuthorizationModelRequest model, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId);
        var response = await client.WriteAuthorizationModel(model,options:null, cancellationToken: ct);
        return response.AuthorizationModelId;
    }

    public async Task<AuthorizationModel?> GetModelAsync(
        string storeId, string modelId, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        var response = await client.ReadAuthorizationModel(
            new ClientReadAuthorizationModelOptions { AuthorizationModelId = modelId },
            cancellationToken: ct);

        return response.AuthorizationModel;
    }

    public async Task<AuthorizationModel?> GetLatestModelAsync(string storeId, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId);
        var response = await client.ReadAuthorizationModels(
            new ClientReadAuthorizationModelsOptions { PageSize = 1 }, cancellationToken: ct);

        return response.AuthorizationModels?.FirstOrDefault();
    }
}
