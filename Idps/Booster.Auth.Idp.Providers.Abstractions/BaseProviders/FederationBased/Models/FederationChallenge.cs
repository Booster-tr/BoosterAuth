namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;

public sealed record FederationChallenge
{
    public required string RedirectUrl { get; init; }

    public required string State { get; init; }
}
