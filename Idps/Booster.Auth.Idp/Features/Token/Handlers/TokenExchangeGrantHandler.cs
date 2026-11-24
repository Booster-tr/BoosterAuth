using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Features.Token.Handlers._base;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.Auth.Idp.Managers.TokenManagers;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Idp.Features.Token.Handlers;

public sealed class TokenExchangeGrantHandler(
    OidcTokenService tokenService,
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    ILocalUserStore principalStore,
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IRequestContextAccessor requestContextAccessor) : IGrantTypeHandler
{
    public OAuthGrantType GrantType => OAuthGrantType.TokenExchange;

    public async Task<TokenResponse> HandleAsync(TokenRequest request, ClientData client, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Resource))
            throw new BusinessException(IdpMessageKeys.ResourceRequiredForExchange);

        if (string.IsNullOrEmpty(request.SubjectToken))
            throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        var claims = tokenService.ValidateToken(request.SubjectToken)
            ?? throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        if (!Guid.TryParse(claims.FindFirst("sub")?.Value, out var principalId))
            throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        // Konu token'i BU istemcinin havuzundan olmali: baska havuzun token'i bu istemciyle baska bir API'ye
        // tasinamaz (havuzlar arasi yetki yukseltme).
        if (!Guid.TryParse(claims.FindFirst("actor")?.Value, out var actor) || actor != client.IsolationId)
            throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        var principalType = claims.FindFirst("principal_type")?.Value ?? PrincipalType.User.ToString();
        var isMachine = string.Equals(principalType, PrincipalType.Machine.ToString(), StringComparison.OrdinalIgnoreCase);
        var subjectAzp = claims.FindFirst("azp")?.Value ?? string.Empty;
        var subjectScope = claims.FindFirst("scope")?.Value ?? string.Empty;

        // Istemcilerin secret'i yok (PKCE'li public client). Istemci, token'in kendisine verildigini kanitlayarak
        // dogrulanir: kullanici token'inda azp bu istemci olmali. Servis hesabi token'inda azp hesabin anahtaridir;
        // hesap asagida ayrica dogrulanir.
        if (!isMachine && !string.Equals(subjectAzp, client.ClientId.ToString(), StringComparison.Ordinal))
            throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        using var scope = requestContextAccessor.BeginSystemContext(actor);

        if (isMachine)
        {
            // Oturumu (sid) olmayan servis hesabi token'i: hesap askiya alinmis, suresi dolmus ya da silinmisse
            // degistirilemez; aksi halde token suresiz yenilenebilirdi.
            var serviceAccount = await serviceAccountRepository.GetAsync(
                x => x.PrincipalId == principalId, enableTracking: false, cancellationToken: ct);

            if (serviceAccount is null
                || !string.Equals(serviceAccount.Key, subjectAzp, StringComparison.Ordinal)
                || serviceAccount.Status != ServiceAccountStatus.Active
                || (serviceAccount.ExpiresAt.HasValue && serviceAccount.ExpiresAt.Value <= DateTimeOffset.UtcNow))
            {
                throw new BusinessException(IdpMessageKeys.ServiceAccountDisabled);
            }
        }

        if (Guid.TryParse(claims.FindFirst("sid")?.Value, out var sessionId))
        {
            var session = await sessionRepository.GetAsync(
                s => s.Id == sessionId, enableTracking: false, cancellationToken: ct);

            if (session is null || session.Status != SessionStatus.Active || session.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new BusinessException(IdpMessageKeys.SessionInvalid);
        }

        if (!client.AllowedAudiences.Any(a =>
                string.Equals(a.Trim(), request.Resource.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(IdpMessageKeys.AudienceNotAllowed);
        }

        var principal = await principalStore.GetByIdAsync(principalId, ct)
            ?? throw new BusinessException(IdpMessageKeys.PrincipalNotFound);
        if (principal.Status != PrincipalStatus.Active)
            throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        // Konu token'indan sonra silinen ya da istemciden kaldirilan kapsamlar tasinmaz.
        var finalScope = ApplyRequestedScope(KeepAllowedScopes(subjectScope, client), request.Scope);

        // Yeni token konu token'indan uzun yasayamaz: degistirme bir yenileme yolu degildir.
        var ttl = client.Session.AccessTokenTtlSeconds;
        if (long.TryParse(claims.FindFirst("exp")?.Value, out var subjectExp))
            ttl = (int)Math.Min(ttl, subjectExp - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (ttl <= 0)
            throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(
            audience: request.Resource,                    
            subject: principalId,                          
            azp: client.ClientId.ToString(),
            scope: finalScope,                             
            roles: ExtractRoles(claims),
            ttlSeconds: ttl,
            principalType: principalType,
            actor: actor.ToString(),
            sessionId: Guid.TryParse(claims.FindFirst("sid")?.Value, out var sid) ? sid : null);

        return new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            IdToken = null,      
            RefreshToken = null, 
            Scope = finalScope
        };
    }

    private static IReadOnlyList<string> ExtractRoles(System.Security.Claims.ClaimsPrincipal claims)
        => claims.FindAll("role").Select(c => c.Value).ToList();

    private static string KeepAllowedScopes(string subjectScope, ClientData client)
        => string.Join(' ', subjectScope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => OidcScopes.IsStandard(s) || client.AllowedScopes.Contains(s, StringComparer.Ordinal)));

    private static string ApplyRequestedScope(string subjectScope, string? requestedScope)
    {
        if (string.IsNullOrWhiteSpace(requestedScope))
            return subjectScope;

        var subject = subjectScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var requested = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (requested.Any(s => !subject.Contains(s)))
            throw new BusinessException(IdpMessageKeys.UpscopingNotAllowed);

        return string.Join(' ', requested);
    }
}