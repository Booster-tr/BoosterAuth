namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;

public sealed record IdentityResult
{
    public required bool Succeeded { get; init; }

    public string? PrincipalId { get; init; }

    public string? ProviderKey { get; init; }

    public IReadOnlyDictionary<string, string> Claims { get; init; }
        = new Dictionary<string, string>();

    public string? FailureReason { get; init; }

    public static IdentityResult Success(
        string principalId,
        string providerKey,
        IReadOnlyDictionary<string, string>? claims = null)
        => new()
        {
            Succeeded = true,
            PrincipalId = principalId,
            ProviderKey = providerKey,
            Claims = claims ?? new Dictionary<string, string>()
        };

    public static IdentityResult Fail(string reason)
        => new() { Succeeded = false, FailureReason = reason };
}
