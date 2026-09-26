using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;

namespace Booster.Auth.Idp.Stores.FederationStores;

public interface IFederationProviderConfigStore
{
    Task<FederationProviderConfig?> GetAsync(Guid userPoolId, string key, CancellationToken ct);
}

public sealed record FederationProviderConfig
{
    public required Guid Id { get; init; }

    public required string Key { get; init; }

    public required string ProviderType { get; init; }

    public required string DisplayName { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyDictionary<string, string> Settings { get; init; }

    public ProviderConfig ToProviderConfig(Guid isolationId) => new()
    {
        ProviderKey = Key,
        ProviderType = ProviderType,
        IsolationId = isolationId,
        IsActive = IsActive,
        Settings = Settings
    };
}
