namespace Booster.Auth.Management.Api.Managers.AccessManagers;

public readonly record struct AccessEntry(Guid PrincipalId, string Relation);

public readonly record struct AccessPage(IReadOnlyList<AccessEntry> Items, string? ContinuationToken);

public interface IAccessManager
{
    Task<AccessPage> ListAsync(
        string objectType, Guid objectId, IReadOnlyList<string> relations,
        int pageSize, string? continuationToken, CancellationToken ct);

    Task GrantAsync(string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct);

    Task RevokeAsync(string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct);

    void RevokeAllInBackground(string objectType, Guid objectId);

    Task<IReadOnlyList<Guid>> ListAccessibleIdsAsync(string objectType, string relation, CancellationToken ct);

    Task LinkParentAsync(string objectType, Guid objectId, string parentType, Guid parentId, CancellationToken ct);
}
