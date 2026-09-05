using System.Text.Json.Serialization;

namespace Booster.Auth.Idp.Features.Token.Handlers.Models;

public record TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = default!;
    [JsonPropertyName("token_type")] public string TokenType { get; init; } = "Bearer";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    [JsonPropertyName("scope")] public string? Scope { get; init; }
}
