using Booster.Auth.Common.Internal.Helpers;
using Booster.Auth.Idp.Grpc;

namespace Booster.Auth.Management.Api.Managers.PoolDefinitionSyncManagers;

/// <summary>
/// Rol / kapsam adi degisikligini ve silmeyi IdP'deki kullanici kopyalarina iletir (gRPC). Cagri kayittan SONRA yapilir
/// ve BEKLENMEZ (<see cref="FireAndForget"/>): IdP kapaliysa kopya bayat kalir, panel islemi etkilenmez.
/// </summary>
public sealed class IdpPoolDefinitionSync(
    PoolDefinitionSyncService.PoolDefinitionSyncServiceClient client,
    ILogger<IdpPoolDefinitionSync> logger)
{
    /// <param name="newKey">Bos = rol silindi.</param>
    public void RoleChanged(Guid userPoolId, Guid roleId, string? newKey = null)
    {
        var request = new RoleChangedRequest
        {
            UserPoolId = userPoolId.ToString(),
            RoleId = roleId.ToString(),
            NewKey = newKey ?? string.Empty
        };

        FireAndForget.Run(logger, "Idp.RoleChanged",
            ct => client.RoleChangedAsync(request, cancellationToken: ct).ResponseAsync);
    }

    /// <param name="newScope">Bos = kapsam silindi.</param>
    public void ScopeDefinitionChanged(Guid userPoolId, string oldScope, string? newScope = null)
    {
        var request = new ScopeDefinitionChangedRequest
        {
            UserPoolId = userPoolId.ToString(),
            OldScope = oldScope,
            NewScope = newScope ?? string.Empty
        };

        FireAndForget.Run(logger, "Idp.ScopeDefinitionChanged",
            ct => client.ScopeDefinitionChangedAsync(request, cancellationToken: ct).ResponseAsync);
    }
}
