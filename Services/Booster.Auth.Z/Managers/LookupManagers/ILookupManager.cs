using Booster.Auth.Z.Managers.LookupManagers.Models;

namespace Booster.Auth.Z.Managers.LookupManagers;

public interface ILookupManager
{
    Task<IReadOnlyList<LookupTuple>> LookupByUserAsync(
        Guid poolId, string user, string objectType, string? relation, CancellationToken ct);

    Task<IReadOnlyList<LookupTuple>> LookupByObjectAsync(
        Guid poolId, string @object, string? relation, CancellationToken ct);

    Task<IReadOnlyList<ObjectAccess>> LookupMyAccessAsync(
        Guid poolId, string user, IReadOnlyList<string> objectTypes, CancellationToken ct);

    Task<ObjectAccess> LookupObjectAccessAsync(
        Guid poolId, string user, string @object, CancellationToken ct);
}
