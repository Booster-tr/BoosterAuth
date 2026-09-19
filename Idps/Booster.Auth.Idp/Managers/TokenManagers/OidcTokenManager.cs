using Booster.Auth.Idp.Managers.KeyManagers;
using Booster.Auth.Idp.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Booster.Auth.Idp.Managers.TokenManagers;

public sealed class OidcTokenService(OidcKeyService keyService, IdpOptions options)
{
    private static readonly JwtSecurityTokenHandler Handler = new() { MapInboundClaims = false };

    public (string Token, int ExpiresIn) CreateAccessToken(
        string audience,
        Guid subject,
        string azp,
        string scope,
        IReadOnlyList<string> roles,
        int ttlSeconds,
        string principalType,
        string actor,
        Guid? sessionId = null)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddSeconds(ttlSeconds);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("azp", azp),
            new("actor", actor),
            new("principal_type", principalType),
            new("scope", scope),
            new("iat", EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
            claims.Add(new Claim("role", role));

        if (sessionId.HasValue)
            claims.Add(new Claim("sid", sessionId.Value.ToString()));

        var token = WriteToken(audience, claims, now, expires);
        return (token, (int)(expires - now).TotalSeconds);
    }

    public string CreateIdToken(
        string audience, Guid subject, string? nonce, string? name, string? email,
        DateTime authTime, int ttlSeconds)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddSeconds(ttlSeconds);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("iat", EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64),
            new("auth_time", EpochTime.GetIntDate(authTime).ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrEmpty(name)) claims.Add(new Claim("name", name));
        if (!string.IsNullOrEmpty(email)) claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        if (!string.IsNullOrEmpty(nonce)) claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));

        return WriteToken(audience, claims, now, expires);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer.TrimEnd('/'),
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keyService.GetValidationKeys(),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };

        try
        {
            return Handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }

    private string WriteToken(string audience, IEnumerable<Claim> claims, DateTime notBefore, DateTime expires)
    {
        var token = new JwtSecurityToken(
            issuer: options.Issuer.TrimEnd('/'),
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: keyService.GetSigningCredentials());

        return Handler.WriteToken(token);
    }
}