namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;

public sealed record FederationContext
{
    public required string RedirectUri { get; init; }

    public required string State { get; init; }

    public string? Nonce { get; init; }

    public string? CodeChallenge { get; init; }

    public string? LoginHint { get; init; }
}

public sealed record FederationChallenge
{
    public required string RedirectUrl { get; init; }

    public required string State { get; init; }
}

public sealed record FederationCallback
{
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public required string RedirectUri { get; init; }

    public string? CodeVerifier { get; init; }

    public string? ExpectedNonce { get; init; }

    public string? Code => Value("code");

    public string? Error => Value("error");

    public string? ErrorDescription => Value("error_description");

    public string? Value(string name)
        => Parameters.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}

public sealed record FederationResult
{
    public required bool Succeeded { get; init; }

    public ExternalIdentity? Identity { get; init; }

    public string? FailureReason { get; init; }

    public static FederationResult Success(ExternalIdentity identity)
        => new() { Succeeded = true, Identity = identity };

    public static FederationResult Fail(string reason)
        => new() { Succeeded = false, FailureReason = reason };
}

public static class FederationFailures
{
    public const string ProviderInactive = "provider_inactive";
    public const string ProviderMisconfigured = "provider_misconfigured";

    public const string AuthorizationDenied = "authorization_denied";

    public const string CodeMissing = "code_missing";
    public const string TokenExchangeFailed = "token_exchange_failed";
    public const string IdTokenInvalid = "id_token_invalid";
    public const string NonceMismatch = "nonce_mismatch";
    public const string ProfileFetchFailed = "profile_fetch_failed";

    public const string SubjectMissing = "subject_missing";

    public const string DiscoveryFailed = "discovery_failed";
    public const string AssertionInvalid = "assertion_invalid";
}
