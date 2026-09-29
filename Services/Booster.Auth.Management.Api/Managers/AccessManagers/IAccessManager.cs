namespace Booster.Auth.Management.Api.Managers.AccessManagers;

public readonly record struct AccessEntry(
    Guid PrincipalId,
    IReadOnlyList<string> Relations);

public interface IAccessManager
{
    Task<IReadOnlyList<AccessEntry>> ListAsync(string objectType, Guid objectId, CancellationToken ct);

    Task GrantAsync(string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct);

    Task RevokeAsync(string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct);

    Task RevokeAllAsync(string objectType, Guid objectId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ListAccessibleIdsAsync(string objectType, CancellationToken ct);

    Task LinkParentAsync(string objectType, Guid objectId, string parentType, Guid parentId, CancellationToken ct);

    Task UnlinkParentAsync(string objectType, Guid objectId, string parentType, Guid parentId, CancellationToken ct);
}
