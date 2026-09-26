using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;

namespace Booster.Auth.Idp.Providers.Apple;

public sealed class AppleProvider(IHttpClientFactory httpClientFactory)
    : OidcFederationProviderBase(httpClientFactory)
{
    public const string Type = "apple";

    public override string ProviderType => Type;

    protected override string? FixedIssuerUrl => "https://appleid.apple.com";

    protected override string DefaultScopes => "openid name email";

    private static readonly TimeSpan SecretLifetime = TimeSpan.FromMinutes(5);

    protected override void ExtendAuthorizationParameters(
        Dictionary<string, string?> parameters, FederationContext context, ProviderConfig config)
    {

        if (Scopes(config).Contains("name", StringComparison.Ordinal)
            || Scopes(config).Contains("email", StringComparison.Ordinal))
        {
            parameters["response_mode"] = "form_post";
        }
    }

    protected override Task<string?> ResolveClientSecretAsync(ProviderConfig config, CancellationToken ct)
    {
        var teamId = Setting(config, FederationSettings.TeamId);
        var keyId = Setting(config, FederationSettings.KeyId);
        var clientId = Setting(config, FederationSettings.ClientId);
        var privateKeyPem = Setting(config, FederationSettings.PrivateKeyPem);

        if (teamId is null || keyId is null || clientId is null || privateKeyPem is null)
            return Task.FromResult<string?>(null);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);

        var securityKey = new ECDsaSecurityKey(ECDsa.Create(ecdsa.ExportParameters(true)))
        {
            KeyId = keyId
        };

        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = teamId,
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", clientId)]),
            Audience = "https://appleid.apple.com",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(SecretLifetime),
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256)
        };

        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };

        return Task.FromResult<string?>(handler.WriteToken(handler.CreateToken(descriptor)));
    }
}
