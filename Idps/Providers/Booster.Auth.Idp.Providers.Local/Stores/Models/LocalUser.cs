namespace Booster.Auth.Idp.Providers.Local.Stores.Models;

public sealed record LocalUser
{
    public required Guid Id { get; init; }

    public required string PasswordHash { get; init; }

    public IReadOnlyDictionary<string, string> Claims { get; init; }
        = new Dictionary<string, string>();

    public bool IsActive { get; init; } = true;

    public int FailedAttemptCount { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }
}
