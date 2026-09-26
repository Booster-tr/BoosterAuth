namespace Booster.Auth.Idp.Stores.FederationStores.Models;

public sealed record FederationStateData
{
    public required string InteractionKey { get; init; }

    public required string ClientKey { get; init; }

    public required Guid IsolationId { get; init; }

    public required Guid IdentityProviderId { get; init; }

    public required string ProviderKey { get; init; }

    public string? CodeVerifier { get; init; }

    public string? Nonce { get; init; }

    public required string RedirectUri { get; init; }
}
