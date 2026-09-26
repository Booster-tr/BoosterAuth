namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;


public sealed record ExternalIdentity
{
    public required string Subject { get; init; }

    public string? Email { get; init; }

    public bool EmailVerified { get; init; }

    public string? DisplayName { get; init; }
}
