using Booster.Auth.Z.Managers.LookupManagers.Models;

namespace Booster.Auth.Z.Managers.LookupManagers;

public interface ILookupManager
{
    Task<IReadOnlyList<LookupTuple>> LookupByUserAsync(
        Guid poolId, string user, string objectType, string? relation, CancellationToken ct);

    Task<IReadOnlyList<LookupTuple>> LookupByObjectAsync(
        Guid poolId, string @object, string? relation, CancellationToken ct);
}
