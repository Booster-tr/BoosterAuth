using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;


public abstract class OidcFederationProviderBase(IHttpClientFactory httpClientFactory)
    : FederationProviderBase(httpClientFactory)
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new()
    {

        MapInboundClaims = false
    };


    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> Managers
        = new(StringComparer.OrdinalIgnoreCase);

    protected virtual string? FixedIssuerUrl => null;


    protected virtual string? FallbackIssuerUrl => null;

    protected virtual bool ValidateNonce => true;

    protected virtual bool UsePkce => true;

    protected virtual void ExtendAuthorizationParameters(
        Dictionary<string, string?> parameters, FederationContext context, ProviderConfig config) { }

    protected string? IssuerUrl(ProviderConfig config)
        => FixedIssuerUrl ?? Setting(config, FederationSettings.IssuerUrl) ?? FallbackIssuerUrl;

    public override async Task<FederationChallenge> BuildChallengeAsync(
        FederationContext context, ProviderConfig config, CancellationToken ct)
    {
        var issuer = IssuerUrl(config);
        if (string.IsNullOrEmpty(issuer))
            throw new InvalidOperationException(FederationFailures.ProviderMisconfigured);

        var discovery = await GetConfigurationAsync(issuer, ct);

        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = Setting(config, FederationSettings.ClientId),
            ["redirect_uri"] = context.RedirectUri,
            ["scope"] = Scopes(config),
            ["state"] = context.State
        };

        if (ValidateNonce && !string.IsNullOrEmpty(context.Nonce))
            parameters["nonce"] = context.Nonce;

        if (UsePkce && !string.IsNullOrEmpty(context.CodeChallenge))
        {
            parameters["code_challenge"] = context.CodeChallenge;
            parameters["code_challenge_method"] = "S256";
        }

        if (Setting(config, FederationSettings.DomainHint) is { } hint)
            parameters["domain_hint"] = hint;

        if (!string.IsNullOrEmpty(context.LoginHint))
            parameters["login_hint"] = context.LoginHint;

        ExtendAuthorizationParameters(parameters, context, config);

        return new FederationChallenge
        {
            RedirectUrl = BuildUrl(discovery.AuthorizationEndpoint, parameters),
            State = context.State
        };
    }

    public override async Task<FederationResult> HandleCallbackAsync(
        FederationCallback callback, ProviderConfig config, CancellationToken ct)
    {
        if (PreflightFailure(callback, config) is { } failure)
            return FederationResult.Fail(failure);

        var issuer = IssuerUrl(config);
        if (string.IsNullOrEmpty(issuer))
            return FederationResult.Fail(FederationFailures.ProviderMisconfigured);

        OpenIdConnectConfiguration discovery;
        try
        {
            discovery = await GetConfigurationAsync(issuer, ct);
        }
        catch (Exception)
        {
            return FederationResult.Fail(FederationFailures.DiscoveryFailed);
        }

        var token = await ExchangeCodeAsync(discovery.TokenEndpoint, callback.Code!, callback, config, ct);
        if (token is null)
            return FederationResult.Fail(FederationFailures.TokenExchangeFailed);

        var idToken = ReadString(token.Value, "id_token");
        if (string.IsNullOrEmpty(idToken))
            return FederationResult.Fail(FederationFailures.IdTokenInvalid);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,

            IssuerValidator = (tokenIssuer, _, _) =>
                IsIssuerMatch(tokenIssuer, Setting(config, FederationSettings.ValidIssuer) ?? discovery.Issuer)
                    ? tokenIssuer
                    : throw new SecurityTokenInvalidIssuerException(tokenIssuer),
            ValidateAudience = true,
            ValidAudience = Setting(config, FederationSettings.ClientId),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = discovery.SigningKeys
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = TokenHandler.ValidateToken(idToken, parameters, out _);
        }
        catch (Exception)
        {
            return FederationResult.Fail(FederationFailures.IdTokenInvalid);
        }


        if (ValidateNonce && !string.IsNullOrEmpty(callback.ExpectedNonce))
        {
            var tokenNonce = principal.FindFirst("nonce")?.Value;
            if (!string.Equals(tokenNonce, callback.ExpectedNonce, StringComparison.Ordinal))
                return FederationResult.Fail(FederationFailures.NonceMismatch);
        }

        var subjectClaim = Setting(config, FederationSettings.SubjectClaim) ?? "sub";
        var subject = principal.FindFirst(subjectClaim)?.Value;

        if (string.IsNullOrEmpty(subject))
            return FederationResult.Fail(FederationFailures.SubjectMissing);

        var email = principal.FindFirst(Setting(config, FederationSettings.EmailClaim) ?? "email")?.Value;
        var name = principal.FindFirst(Setting(config, FederationSettings.NameClaim) ?? "name")?.Value;

        var verifiedClaim = Setting(config, FederationSettings.EmailVerifiedClaim) ?? "email_verified";
        var emailVerified = bool.TryParse(principal.FindFirst(verifiedClaim)?.Value, out var parsed) && parsed;

        return FederationResult.Success(new ExternalIdentity
        {
            Subject = subject,
            Email = email,
            EmailVerified = emailVerified,
            DisplayName = name
        });
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string issuerUrl, CancellationToken ct)
    {
        var manager = Managers.GetOrAdd(issuerUrl, url =>
        {
            var metadataAddress = $"{url.TrimEnd('/')}/.well-known/openid-configuration";

            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(CreateClient()) { RequireHttps = true });
        });

        return await manager.GetConfigurationAsync(ct);
    }

    protected static bool IsIssuerMatch(string tokenIssuer, string expectedIssuer)
    {
        tokenIssuer = tokenIssuer.TrimEnd('/');
        expectedIssuer = expectedIssuer.TrimEnd('/');

        if (string.Equals(tokenIssuer, expectedIssuer, StringComparison.OrdinalIgnoreCase))
            return true;

        var open = expectedIssuer.IndexOf('{');
        var close = expectedIssuer.IndexOf('}');

        if (open < 0 || close < open)
            return false;

        var prefix = expectedIssuer[..open];
        var suffix = expectedIssuer[(close + 1)..];

        return tokenIssuer.Length > prefix.Length + suffix.Length
            && tokenIssuer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && tokenIssuer.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
}
