using Booster.Auth.Database.Models;
using Booster.Auth.Management.Grpc;

namespace Booster.Auth.Idp.Managers.UserManagers;

/// <summary>
/// Istemcisiz akislarda (panelden ekleme, davet kabulu) havuz politikasi ve varsayilan rol/kapsamlar.
/// Onbellege alinmaz: bu akislar seyrek calisir ve politikanin GUNCEL hali gerekir (or. yeni parola kurali).
/// </summary>
public sealed class PoolDataReader(ClientDataService.ClientDataServiceClient client)
{
    public async Task<ClientData?> GetAsync(Guid poolId, CancellationToken ct)
    {
        var r = await client.GetPoolDataAsync(new GetPoolDefinitionsRequest { UserPoolId = poolId.ToString() }, cancellationToken: ct);
        return r.Found ? ClientDataMapper.ToModel(r.Client) : null;
    }
}
