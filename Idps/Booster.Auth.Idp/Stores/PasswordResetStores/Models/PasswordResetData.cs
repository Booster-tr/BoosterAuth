namespace Booster.Auth.Idp.Stores.PasswordResetStores.Models;


public sealed record PasswordResetData
{
    public required Guid PrincipalId { get; init; }

    public required Guid IsolationId { get; init; }

    public required string ClientKey { get; init; }

    public required string CodeHash { get; init; }

    public int AttemptCount { get; init; }
}
