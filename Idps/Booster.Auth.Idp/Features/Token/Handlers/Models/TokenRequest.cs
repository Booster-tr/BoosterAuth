using FastEndpoints;

namespace Booster.Auth.Idp.Features.Token.Handlers.Models;

public record TokenRequest
{
    [BindFrom("grant_type")] public string GrantType { get; init; } = default!;
    [BindFrom("client_id")] public string? ClientKey { get; init; }
    [BindFrom("service_account_secret")] public string? ServiceAccountSecret { get; init; }
    [BindFrom("service_account_key")] public string? ServiceAccountKey { get; init; }

    // authorization_code
    [BindFrom("code")] public string? Code { get; init; }
    [BindFrom("redirect_uri")] public string? RedirectUri { get; init; }
    [BindFrom("code_verifier")] public string? CodeVerifier { get; init; }   // PKCE

    // refresh_token
    [BindFrom("refresh_token")] public string? RefreshToken { get; init; }

    // token_exchange (RFC 8693)
    [BindFrom("subject_token")] public string? SubjectToken { get; init; }
    [BindFrom("subject_token_type")] public string? SubjectTokenType { get; init; }
    [BindFrom("requested_token_type")] public string? RequestedTokenType { get; init; }

    // Ortak
    [BindFrom("scope")] public string? Scope { get; init; }
    [BindFrom("resource")] public string? Resource { get; init; }           // audience
}
