namespace Booster.Auth.Management.Api.Managers.PrincipalLookupManagers;

public interface IPrincipalLookupManager
{
    Task<Guid> FindAsync(string identifier, CancellationToken ct);
}
