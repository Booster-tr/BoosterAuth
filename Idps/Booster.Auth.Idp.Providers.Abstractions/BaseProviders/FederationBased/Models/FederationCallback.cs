namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;

public sealed record FederationCallback
{
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}
