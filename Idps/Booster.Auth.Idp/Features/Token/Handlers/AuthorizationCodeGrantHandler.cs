using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features.Token.Handlers._base;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.Auth.Idp.Managers.TokenManagers;
using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.OneWays.Simple;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Booster.Auth.Idp.Features.Token.Handlers;

public sealed class AuthorizationCodeGrantHandler(
    ISecureCacheStore<AuthorizationCodeData> codeStore,
    OidcTokenService tokenService,
    ILocalUserStore principalStore,
    IAsyncRepository<PrincipalRole, IdpDbContext> roleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> scopeRepository,
    IAsyncRepository<RefreshToken, IdpDbContext> refreshTokenRepository,
    ISimpleHasher simpleHasher,
    IRequestContextAccessor requestContextAccessor) : IGrantTypeHandler
{
    public OAuthGrantType GrantType => OAuthGrantType.AuthorizationCode;

    public async Task<TokenResponse> HandleAsync(TokenRequest request, ClientData client, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Code))
            throw new BusinessException(IdpMessageKeys.InvalidGrant);

        var authCode = await codeStore.ConsumeAsync(request.Code, ct);
        if (authCode is null || authCode.ClientId != client.ClientId)
            throw new BusinessException(IdpMessageKeys.InvalidGrant);

        if (!string.Equals(authCode.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
            throw new BusinessException(IdpMessageKeys.InvalidRedirectUri);

        VerifyPkce(request.CodeVerifier, authCode.CodeChallenge);

        using var scope = requestContextAccessor.BeginSystemContext(authCode.IsolationId);

        var principal = await principalStore.GetByIdAsync(authCode.PrincipalId, ct)
            ?? throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        var roles = await roleRepository.Query()
            .Where(r => r.PrincipalId == principal.Id)
            .Select(r => r.RoleName)
            .ToListAsync(ct);

        var principalScopes = await scopeRepository.Query()
            .Where(s => s.PrincipalId == principal.Id)
            .Select(s => s.Scope)
            .ToListAsync(ct);

        var grantedScope = MergeScopes(authCode.Scope, principalScopes);

        var finalScope = ApplyRequestedScope(grantedScope, request.Scope);

        var accessTtl = client.Session.AccessTokenTtlSeconds;
        var refreshTtl = client.Session.RefreshTokenTtlSeconds;

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(
            audience: request.Resource ?? string.Empty,
            subject: principal.Id,
            azp: client.ClientId.ToString(),
            scope: finalScope,
            roles: roles,
            ttlSeconds: accessTtl,
            principalType: principal.Type.ToString(),
            actor: authCode.IsolationId.ToString(),
            sessionId: authCode.SessionId);

        var idToken = tokenService.CreateIdToken(
            audience: client.ClientKey,
            subject: principal.Id,
            nonce: authCode.Nonce,
            name: principal.DisplayName,
            email: principal.Email,
            authTime: DateTime.UtcNow,
            ttlSeconds: accessTtl);

        var refreshString = await IssueRefreshTokenAsync(authCode, client, finalScope, roles, refreshTtl, ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            IdToken = idToken,
            RefreshToken = refreshString,
            Scope = finalScope
        };
    }

    private static string ApplyRequestedScope(string grantedScope, string? requestedScope)
    {
        if (string.IsNullOrWhiteSpace(requestedScope))
            return grantedScope;

        var granted = grantedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        var requested = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (requested.Any(s => !granted.Contains(s)))
            throw new BusinessException(IdpMessageKeys.UpscopingNotAllowed);

        return string.Join(' ', requested);
    }

    private async Task<string> IssueRefreshTokenAsync(
        AuthorizationCodeData authCode, ClientData client, string scope,
        IReadOnlyList<string> roles, int refreshTtl, CancellationToken ct)
    {
        var token = GenerateToken();

        var entity = new RefreshToken
        {
            Id = EntityId.New(),
            PrincipalId = authCode.PrincipalId,
            SessionId = authCode.SessionId,
            ClientId = client.ClientId,
            TokenHash = simpleHasher.Hash(token),
            GrantedScopes = scope,
            GrantedRoles = string.Join(' ', roles),
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshTtl),
            CreatedTime = DateTimeOffset.UtcNow
        };

        await refreshTokenRepository.AddAsync(entity, ct);
        await refreshTokenRepository.SaveChangesAsync(ct);

        return token;
    }

    private static void VerifyPkce(string? codeVerifier, string? codeChallenge)
    {
        if (string.IsNullOrEmpty(codeChallenge)
            || string.IsNullOrEmpty(codeVerifier)
            || codeVerifier.Length is < 43 or > 128)
        {
            throw new BusinessException(IdpMessageKeys.PkceValidationFailed);
        }

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Base64UrlEncoder.Encode(hash);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(computed),
                Encoding.ASCII.GetBytes(codeChallenge)))
        {
            throw new BusinessException(IdpMessageKeys.PkceValidationFailed);
        }
    }

    private static string MergeScopes(string codeScope, IReadOnlyList<string> principalScopes)
    {
        var fromCode = codeScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', fromCode.Union(principalScopes, StringComparer.Ordinal));
    }

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}