using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Managers.KeyManagers;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Booster.Auth.Idp.Features.Logout;

public class LogoutEndpoint(
    IClientDataStore clientDataStore,
    ISessionStore sessionStore,
    ISessionCookieManager cookieManager,
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    IAsyncRepository<RefreshToken, IdpDbContext> refreshTokenRepository,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor,
    IdpUiOptions uiOptions,
    OidcKeyService keyService) : Endpoint<LogoutRequest>
{
    public override void Configure()
    {
        Verbs(Http.GET, Http.POST);
        Routes("logout");
        AllowAnonymous();
        Summary(s => s.Summary = "OIDC end session endpoint (oturumu kapatır)");
    }

    public override async Task HandleAsync(LogoutRequest req, CancellationToken ct)
    {
        var token = cookieManager.Get(HttpContext.Request);
        var session = string.IsNullOrEmpty(token) ? null : await sessionStore.ResolveAsync(token, ct);

        // "Tum cihazlardan cik" yalnizca bu kullaniciya ait imzali id_token_hint ile ya da POST ile: aksi halde herhangi
        // bir site gizli bir GET ile kullaniciyi tum cihazlardan cikarabiliyordu. Tek oturumun kapanmasi OIDC geregi
        // ipucusuz da olur.
        if (session is not null)
        {
            var logoutAll = req.LogoutAll
                && (HttpMethods.IsPost(HttpContext.Request.Method) || SubjectFromIdToken(req.IdTokenHint) == session.PrincipalId.ToString());
            await RevokeAsync(session, logoutAll, ct);
        }

        cookieManager.Clear(HttpContext.Response);

        await RedirectAsync(await ResolveReturnUrlAsync(req, ct));
    }

    private async Task RevokeAsync(Session session, bool logoutAll, CancellationToken ct)
    {
        using var scope = requestContextAccessor.BeginSystemContext(session.IsolationId);

        var now = DateTimeOffset.UtcNow;

        var sessions = logoutAll
                       ? (await sessionRepository.ListAsync(
                           s => s.PrincipalId == session.PrincipalId && s.Status == SessionStatus.Active,
                           index: 0, size: int.MaxValue, cancellationToken: ct)).Items
                       : await sessionRepository.GetAsync(s => s.Id == session.Id, cancellationToken: ct) is { } current
                           ? [current]
                           : [];

        if (sessions.Count == 0)
            return;

        var sessionIds = sessions.Select(s => s.Id).ToList();

        foreach (var item in sessions)
        {
            item.Status = SessionStatus.Revoked;
            item.RevokedAt = now;
            sessionRepository.Update(item);
        }

        var tokens = await refreshTokenRepository.ListAsync(r => sessionIds.Contains(r.SessionId) && r.RevokedAt == null, cancellationToken: ct);

        foreach (var item in tokens.Items)
        {
            item.RevokedAt = now;
            refreshTokenRepository.Update(item);
        }

        await sessionRepository.SaveChangesAsync(ct);

        foreach (var id in sessionIds)
            await cacheService.RemoveByTagAsync(SessionStore.Tag(id), ct);
    }

    private async Task<string> ResolveReturnUrlAsync(LogoutRequest req, CancellationToken ct)
    {
        var fallback = uiOptions.GetErrorUrl(IdpMessageKeys.LoggedOut);

        // OIDC'de client_id istege bagli; yoksa id_token_hint'in aud'u kullanilir (or. .NET OIDC handler).
        var clientKey = string.IsNullOrEmpty(req.ClientKey) ? ClientKeyFromIdToken(req.IdTokenHint) : req.ClientKey;

        if (string.IsNullOrEmpty(clientKey) || string.IsNullOrEmpty(req.PostLogoutRedirectUri))
            return fallback;

        var client = await clientDataStore.GetByClientKeyAsync(clientKey, ct);
        if (client is null)
            return fallback;

        if (!client.PostLogoutRedirectUris.Contains(req.PostLogoutRedirectUri, StringComparer.Ordinal))
            return fallback;

        if (string.IsNullOrEmpty(req.State))
            return req.PostLogoutRedirectUri;

        var builder = new UriBuilder(req.PostLogoutRedirectUri);
        var state = $"state={Uri.EscapeDataString(req.State)}";
        builder.Query = string.IsNullOrEmpty(builder.Query)
            ? state
            : $"{builder.Query.TrimStart('?')}&{state}";

        return builder.ToString();
    }

    private string? ClientKeyFromIdToken(string? idTokenHint) => ReadIdTokenHint(idTokenHint)?.Audiences.FirstOrDefault();

    private string? SubjectFromIdToken(string? idTokenHint) => ReadIdTokenHint(idTokenHint)?.Subject;

    /// <summary>Imza bizim anahtarimizla dogrulanir; suresi dolmus ipucu gecerlidir (OIDC RP-Initiated Logout).</summary>
    private JwtSecurityToken? ReadIdTokenHint(string? idTokenHint)
    {
        if (string.IsNullOrEmpty(idTokenHint))
            return null;

        try
        {
            new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(idTokenHint, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKeys = keyService.GetValidationKeys()
            }, out var token);

            return (JwtSecurityToken)token;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Task RedirectAsync(string url)
        => Send.RedirectAsync(url, isPermanent: false, allowRemoteRedirects: true);
}

public record LogoutRequest
{
    [BindFrom("client_id")] public string? ClientKey { get; init; }
    [BindFrom("post_logout_redirect_uri")] public string? PostLogoutRedirectUri { get; init; }
    [BindFrom("id_token_hint")] public string? IdTokenHint { get; init; }
    [BindFrom("state")] public string? State { get; init; }
    [BindFrom("logout_all")] public bool LogoutAll { get; init; }
}
