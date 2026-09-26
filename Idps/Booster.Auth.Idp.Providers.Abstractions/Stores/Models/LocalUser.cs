using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Idp.Providers.Abstractions.Stores.Models;

public sealed record LocalUser
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public required PrincipalType Type { get; init; }

    public required PrincipalStatus Status { get; init; }

    public bool IsActive => Status == PrincipalStatus.Active;

    public string? UserName { get; init; }

    public string? Email { get; init; }
    public bool EmailVerified { get; init; }

    public string? PhoneNumber { get; init; }
    public bool PhoneVerified { get; init; }

    public string? PasswordHash { get; init; }

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public int FailedAttemptCount { get; init; }

    public DateTimeOffset? LockedUntil { get; init; }

    public IReadOnlyDictionary<string, string> Claims { get; init; }
        = new Dictionary<string, string>();
}
