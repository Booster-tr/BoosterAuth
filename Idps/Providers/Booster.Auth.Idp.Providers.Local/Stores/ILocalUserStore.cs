using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Local.Stores.Models;

namespace Booster.Auth.Idp.Providers.Local.Stores;

public interface ILocalUserStore
{
    Task<LocalUser?> FindAsync(
    string identifier,
    IReadOnlyList<IdentifierType> allowedIdentifiers,
    CancellationToken ct);

    Task UpdatePasswordHashAsync(Guid principalId, string newHash, CancellationToken ct);
}
