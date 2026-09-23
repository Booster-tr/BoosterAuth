using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Constants;
using FastEndpoints;
using System.Text.Json.Serialization;

namespace Booster.Auth.Idp.Features.OidcDiscovery.Discovery;

public class DiscoveryEndpoint(IdpOptions options) : EndpointWithoutRequest<DiscoveryResponse>
{
    public override void Configure()
    {
        Get("/.well-known/openid-configuration");
        AllowAnonymous();
        Summary(s => s.Summary = "OIDC discovery document");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var issuer = options.Issuer.TrimEnd('/');

        var response = new DiscoveryResponse
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/authorize",
            TokenEndpoint = $"{issuer}/token",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            UserInfoEndpoint = $"{issuer}/me",
            EndSessionEndpoint = $"{issuer}/logout",
            ResponseTypesSupported = ["code"],
            GrantTypesSupported =
            [
                "authorization_code",
                "refresh_token",
                "client_credentials",
                "urn:ietf:params:oauth:grant-type:token-exchange"
            ],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"],
            ScopesSupported = OidcScopes.All,
            TokenEndpointAuthMethodsSupported = ["client_secret_post"],
            ClaimsSupported = ["sub", "booster:panel:access", "offline_access", "azp", "scope", "aud", "jti", "iat", "exp", "nbf", "name", "email", "auth_time", "nonce"],
            CodeChallengeMethodsSupported = ["S256"]
        };

        await Send.OkAsync(response, ct);
    }
}

public record DiscoveryResponse
{
    [JsonPropertyName("issuer")] public string Issuer { get; init; } = default!;
    [JsonPropertyName("authorization_endpoint")] public string AuthorizationEndpoint { get; init; } = default!;
    [JsonPropertyName("token_endpoint")] public string TokenEndpoint { get; init; } = default!;
    [JsonPropertyName("jwks_uri")] public string JwksUri { get; init; } = default!;
    [JsonPropertyName("userinfo_endpoint")] public string UserInfoEndpoint { get; init; } = default!;
    [JsonPropertyName("end_session_endpoint")] public string EndSessionEndpoint { get; init; } = default!;
    [JsonPropertyName("response_types_supported")] public string[] ResponseTypesSupported { get; init; } = [];
    [JsonPropertyName("grant_types_supported")] public string[] GrantTypesSupported { get; init; } = [];
    [JsonPropertyName("subject_types_supported")] public string[] SubjectTypesSupported { get; init; } = [];
    [JsonPropertyName("id_token_signing_alg_values_supported")] public string[] IdTokenSigningAlgValuesSupported { get; init; } = [];
    [JsonPropertyName("scopes_supported")] public string[] ScopesSupported { get; init; } = [];
    [JsonPropertyName("token_endpoint_auth_methods_supported")] public string[] TokenEndpointAuthMethodsSupported { get; init; } = [];
    [JsonPropertyName("claims_supported")] public string[] ClaimsSupported { get; init; } = [];
    [JsonPropertyName("code_challenge_methods_supported")] public string[] CodeChallengeMethodsSupported { get; init; } = [];
}
