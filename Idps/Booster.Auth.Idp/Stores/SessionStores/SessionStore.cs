using Booster.Auth.Common.Internal.Auditing;
﻿using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.Hashing.OneWays.Simple;
using System.Security.Cryptography;

namespace Booster.Auth.Idp.Stores.SessionStores;

public sealed class SessionStore(
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    ICacheService cacheService,
    ISimpleHasher simpleHasher,
    AuditLog audit) : ISessionStore
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static string CacheKey(string tokenHash) => $"idp:session:{tokenHash}";
    public static string Tag(Guid sessionId) => $"session:{sessionId}";

    public async Task<(Session Session, string Token)> CreateAsync(
        Guid principalId, Guid clientId, TimeSpan ttl,
        string? deviceName, string? userAgent, string? ipAddress, string? authMethod,
        CancellationToken ct)
    {
        var token = GenerateToken();
        var tokenHash = simpleHasher.Hash(token);

        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            Id = EntityId.New(), 
            PrincipalId = principalId,
            ClientId = clientId,
            TokenHash = tokenHash,
            Status = SessionStatus.Active,
            DeviceName = deviceName,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            AuthMethod = authMethod,
            LastActivityAt = now,
            ExpiresAt = now.Add(ttl),
            CreatedTime = now
        };

        await sessionRepository.AddAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        // Her basarili giris (parola, LDAP, kod, federasyon, kayit, davet) tek noktadan denetim kaydina.
        audit.Success("auth.signin", new AuditEntry(PrincipalId: principalId, PoolId: session.IsolationId, ClientId: clientId, Method: authMethod));

        return (session, token);
    }

    public async Task<Session?> ResolveAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var tokenHash = simpleHasher.Hash(token);
        var key = CacheKey(tokenHash);

        var session = await cacheService.GetOrSetAsync(
            key,
            async (context, innerCt) =>
            {
                var found = await sessionRepository.GetAsync(
                    predicate: s => s.TokenHash == tokenHash && s.DeletedTime == null,
                    withoutGlobalFilter: true,
                    enableTracking: false,
                    cancellationToken: innerCt);

                if (found is null)
                    return null;

                context.SetTags(Tag(found.Id));
                return found;
            },
            expiry: CacheTtl,
            ct: ct);

        if (session is null)
            return null;

        if (session.Status != SessionStatus.Active || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        return session;
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await sessionRepository.GetAsync(s => s.Id == sessionId, cancellationToken: ct);
        if (session is null)
            return;

        session.Status = SessionStatus.Revoked;
        session.RevokedAt = DateTimeOffset.UtcNow;
        sessionRepository.Update(session);
        await sessionRepository.SaveChangesAsync(ct);

        await cacheService.RemoveByTagAsync(Tag(sessionId), ct);
    }

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
