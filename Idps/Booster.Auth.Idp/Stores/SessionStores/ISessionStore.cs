using Booster.Auth.Idp.Features._base.Entities;

namespace Booster.Auth.Idp.Stores.SessionStores;

public interface ISessionStore
{
    Task<(Session Session, string Token)> CreateAsync(
        Guid principalId, Guid clientId, TimeSpan ttl,
        string? deviceName, string? userAgent, string? ipAddress, string? authMethod,
        CancellationToken ct);

    Task<Session?> ResolveAsync(string token, CancellationToken ct);

    Task RevokeAsync(Guid sessionId, CancellationToken ct);
}