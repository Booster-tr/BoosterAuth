using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Features.Token.Handlers._base;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.Auth.Idp.Managers.TokenManagers;
using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.OneWays.Simple;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Booster.Auth.Idp.Features.Token.Handlers;

public sealed class RefreshTokenGrantHandler(
    OidcTokenService tokenService,
    ILocalUserStore principalStore,
    ISessionStore sessionStore,
    ISimpleHasher simpleHasher,
    IAsyncRepository<RefreshToken, IdpDbContext> refreshTokenRepository,
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    IRequestContextAccessor requestContextAccessor) : IGrantTypeHandler
{
    public OAuthGrantType GrantType => OAuthGrantType.RefreshToken;

    public async Task<TokenResponse> HandleAsync(TokenRequest request, ClientData client, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            throw new BusinessException(IdpMessageKeys.InvalidGrant);

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var tokenHash = simpleHasher.Hash(request.RefreshToken);

        var stored = await refreshTokenRepository.GetAsync(
            r => r.TokenHash == tokenHash, cancellationToken: ct);

        if (stored is null)
            throw new BusinessException(IdpMessageKeys.InvalidGrant);

        if (stored.ClientId != client.ClientId)
            throw new BusinessException(IdpMessageKeys.InvalidGrant);

        if (stored.RevokedAt is not null)
        {
            await RevokeAllSessionTokensAsync(stored.SessionId, ct);
            throw new BusinessException(IdpMessageKeys.RefreshTokenReuseDetected);
        }

        if (stored.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new BusinessException(IdpMessageKeys.RefreshTokenExpired);

        var session = await sessionRepository.GetAsync(
            s => s.Id == stored.SessionId, cancellationToken: ct);

        if (session is null || session.Status != SessionStatus.Active || session.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new BusinessException(IdpMessageKeys.SessionInvalid);

        var principal = await principalStore.GetByIdAsync(stored.PrincipalId, ct)
            ?? throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        var scopeString = ApplyRequestedScope(stored.GrantedScopes, request.Scope);

        var roles = string.IsNullOrWhiteSpace(stored.GrantedRoles)
            ? Array.Empty<string>()
            : stored.GrantedRoles.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var accessTtl = client.Session.AccessTokenTtlSeconds;
        var refreshTtl = client.Session.RefreshTokenTtlSeconds;

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(
            audience: request.Resource ?? string.Empty,
            subject: principal.Id,
            azp: client.ClientId.ToString(),
            scope: scopeString,
            roles: roles,
            ttlSeconds: accessTtl,
            principalType: principal.Type.ToString(),
            actor: stored.IsolationId.ToString(),
            sessionId: session.Id);

        var newRefreshString = await RotateAsync(stored, session, refreshTtl, ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            RefreshToken = newRefreshString,
            Scope = scopeString
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

    private async Task<string> RotateAsync(RefreshToken current, Session session, int refreshTtl, CancellationToken ct)
    {
        var newToken = GenerateToken();
        var newEntity = new RefreshToken
        {
            Id = EntityId.New(),
            PrincipalId = current.PrincipalId,
            SessionId = current.SessionId,
            ClientId = current.ClientId,
            TokenHash = simpleHasher.Hash(newToken),
            GrantedScopes = current.GrantedScopes,
            GrantedRoles = current.GrantedRoles,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshTtl),
            CreatedTime = DateTimeOffset.UtcNow
        };

        current.RevokedAt = DateTimeOffset.UtcNow;
        current.ReplacedByTokenId = newEntity.Id;

        session.LastActivityAt = DateTimeOffset.UtcNow;

        refreshTokenRepository.Update(current);
        await refreshTokenRepository.AddAsync(newEntity, ct);
        sessionRepository.Update(session);
        await refreshTokenRepository.SaveChangesAsync(ct);

        return newToken;
    }

    private async Task RevokeAllSessionTokensAsync(Guid sessionId, CancellationToken ct)
    {
        var tokens = await refreshTokenRepository.Query()
            .Where(r => r.SessionId == sessionId && r.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var t in tokens)
            t.RevokedAt = now;

        var session = await sessionRepository.GetAsync(s => s.Id == sessionId, cancellationToken: ct);
        if (session is not null)
        {
            session.Status = SessionStatus.Revoked;
            session.RevokedAt = now;
            sessionRepository.Update(session);
            await sessionStore.RevokeAsync(sessionId, ct);
        }

        refreshTokenRepository.UpdateRange(tokens);
        await refreshTokenRepository.SaveChangesAsync(ct);
    }

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
