namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;

public sealed record FederationContext
{
    public required string ReturnUrl { get; init; }

    public string? State { get; init; }
}