using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Features.Token.Handlers._base;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.Auth.Idp.Managers.TokenManagers;
using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Idp.Stores.ClientStores.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Idp.Features.Token.Handlers;

public sealed class TokenExchangeGrantHandler(
    OidcTokenService tokenService,
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    ILocalUserStore principalStore,
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

        var isolation = Guid.TryParse(claims.FindFirst("isolation")?.Value, out var iso)
            ? iso
            : Guid.Empty;
        if (isolation == Guid.Empty)
            throw new BusinessException(IdpMessageKeys.InvalidSubjectToken);

        var principalType = claims.FindFirst("principal_type")?.Value ?? PrincipalType.User.ToString();
        var subjectScope = claims.FindFirst("scope")?.Value ?? string.Empty;

        using var scope = requestContextAccessor.BeginSystemContext(isolation);

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

        var finalScope = ApplyRequestedScope(subjectScope, request.Scope);

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(
            audience: request.Resource,                    
            subject: principalId,                          
            azp: client.ClientId.ToString(),
            scope: finalScope,                             
            roles: ExtractRoles(claims),
            ttlSeconds: client.Session.AccessTokenTtlSeconds,
            principalType: principalType,
            isolation: isolation,
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