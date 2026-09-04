namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;

public sealed record ProviderConfig
{
    public required string ProviderKey { get; init; }

    public required string ProviderType { get; init; }

    public required Guid IsolationId { get; init; }

    public required bool IsActive { get; init; }

    public IReadOnlyDictionary<string, string> Settings { get; init; }
        = new Dictionary<string, string>();
}
