namespace Booster.Auth.Management.Api.Managers.AccessManagers;

public readonly record struct AccessEntry(
    Guid PrincipalId,
    IReadOnlyList<string> Granted,
    IReadOnlyList<string> Effective);

public interface IAccessManager
{
    Task<IReadOnlyList<AccessEntry>> ListAsync(string objectType, Guid objectId, CancellationToken ct);

    Task GrantAsync(string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct);

    Task RevokeAsync(string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct);

    Task RevokeAllAsync(string objectType, Guid objectId, CancellationToken ct);
}
