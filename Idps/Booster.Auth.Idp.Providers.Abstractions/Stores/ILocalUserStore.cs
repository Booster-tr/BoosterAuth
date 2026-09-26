using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;

namespace Booster.Auth.Idp.Providers.Abstractions.Stores;

public interface ILocalUserStore
{
    Task<LocalUser?> FindAsync(
        string identifier,
        IReadOnlyList<IdentifierType> identifierTypes,
        CancellationToken ct);

    Task<LocalUser?> GetByIdAsync(Guid id, CancellationToken ct);

    Task UpdatePasswordHashAsync(Guid principalId, string newHash, CancellationToken ct);

    Task MarkIdentifierVerifiedAsync(
        Guid principalId,
        IdentifierType identifierType,
        CancellationToken ct);
}
