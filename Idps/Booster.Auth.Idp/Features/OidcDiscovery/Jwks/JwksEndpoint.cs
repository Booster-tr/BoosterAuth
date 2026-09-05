using Booster.Auth.Idp.Managers.KeyManagers;
using FastEndpoints;
using System.Text.Json.Serialization;

namespace Booster.Auth.Idp.Features.OidcDiscovery.Jwks;

public class JwksEndpoint(OidcKeyService keyService) : EndpointWithoutRequest<JwksResponse>
{
    public override void Configure()
    {
        Get("/.well-known/jwks.json");
        AllowAnonymous();
        Summary(s => s.Summary = "JSON Web Key Set");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var keys = keyService.GetPublicJsonWebKeys()
            .Select(jwk => new JwkModel
            {
                Kty = jwk.Kty,
                Use = jwk.Use,
                Kid = jwk.Kid,
                Alg = jwk.Alg,
                N = jwk.N,
                E = jwk.E
            })
            .ToArray();

        await Send.OkAsync(new JwksResponse { Keys = keys }, ct);
    }
}

public record JwksResponse
{
    [JsonPropertyName("keys")] public JwkModel[] Keys { get; init; } = [];
}

public record JwkModel
{
    [JsonPropertyName("kty")] public string Kty { get; init; } = default!;
    [JsonPropertyName("use")] public string Use { get; init; } = default!;
    [JsonPropertyName("kid")] public string Kid { get; init; } = default!;
    [JsonPropertyName("alg")] public string Alg { get; init; } = default!;
    [JsonPropertyName("n")] public string N { get; init; } = default!;
    [JsonPropertyName("e")] public string E { get; init; } = default!;
}
