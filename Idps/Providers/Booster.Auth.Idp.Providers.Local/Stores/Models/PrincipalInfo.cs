using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Idp.Providers.Local.Stores.Models;

public sealed record PrincipalInfo
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public bool EmailVerified { get; init; }
    public string? UserName { get; init; }
    public string? PhoneNumber { get; init; }
    public bool PhoneVerified { get; init; }
    public required PrincipalType Type { get; init; }
    public required PrincipalStatus Status { get; init; }
}