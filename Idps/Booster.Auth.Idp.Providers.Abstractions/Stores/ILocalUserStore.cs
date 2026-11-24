using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;

namespace Booster.Auth.Idp.Providers.Abstractions.Stores;

public interface ILocalUserStore
{
    /// <summary>Kimlik <see cref="IdentifierNormalizer"/> ile bicimlenip aranir; telefonda ulke kodsuz numaraya
    /// <paramref name="defaultCountryCode"/> eklenir.</summary>
    Task<LocalUser?> FindAsync(
        string identifier,
        IReadOnlyList<IdentifierType> identifierTypes,
        CancellationToken ct,
        string? defaultCountryCode = null);

    Task<LocalUser?> GetByIdAsync(Guid id, CancellationToken ct);

    Task UpdatePasswordHashAsync(Guid principalId, string newHash, CancellationToken ct);

    Task MarkIdentifierVerifiedAsync(
        Guid principalId,
        IdentifierType identifierType,
        CancellationToken ct);
}
