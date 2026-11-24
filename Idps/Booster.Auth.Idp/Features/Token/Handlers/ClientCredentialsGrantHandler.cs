using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Features.Token.Handlers._base;
﻿using Booster.Auth.Database.Models.Enums;
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
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.OneWays.Simple;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Token.Handlers;

public sealed class ClientCredentialsGrantHandler(
    OidcTokenService tokenService,
    ILocalUserStore principalStore,
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> roleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> scopeRepository,
    ISimpleHasher simpleHasher,
    IdpOptions idpOptions,
    IRequestContextAccessor requestContextAccessor) : IGrantTypeHandler
{
    public OAuthGrantType GrantType => OAuthGrantType.ClientCredentials;

    public async Task<TokenResponse> HandleAsync(TokenRequest request, ClientData client, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ServiceAccountKey) || string.IsNullOrEmpty(request.ServiceAccountSecret))
            throw new BusinessException(IdpMessageKeys.ServiceAccountKeyRequired);

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var serviceAccount = await serviceAccountRepository.GetAsync(
            x => x.Key == request.ServiceAccountKey, enableTracking: false, cancellationToken: ct);

        if (serviceAccount is null || !simpleHasher.Verify(request.ServiceAccountSecret, serviceAccount.SecretHash))
            throw new BusinessException(IdpMessageKeys.InvalidServiceAccount);

        if (serviceAccount.IsolationId != client.IsolationId)
            throw new BusinessException(IdpMessageKeys.InvalidServiceAccount);

        if (serviceAccount.Status != ServiceAccountStatus.Active
            || (serviceAccount.ExpiresAt.HasValue && serviceAccount.ExpiresAt.Value <= DateTimeOffset.UtcNow))
        {
            throw new BusinessException(IdpMessageKeys.ServiceAccountDisabled);
        }

        var principal = await principalStore.GetByIdAsync(serviceAccount.PrincipalId, ct)
            ?? throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        if (principal.Status != PrincipalStatus.Active)
            throw new BusinessException(IdpMessageKeys.ServiceAccountDisabled);

        var roles = await roleRepository.Query()
            .Where(r => r.PrincipalId == principal.Id)
            .Select(r => r.RoleName)
            .ToListAsync(ct);

        var principalScopes = await scopeRepository.Query()
            .Where(s => s.PrincipalId == principal.Id)
            .Select(s => s.Scope)
            .ToListAsync(ct);

        var grantedScope = MergeScopes(client.AllowedScopes, principalScopes);
        var finalScope = ApplyRequestedScope(grantedScope, request.Scope);

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(
            audience: TokenGrantRules.ResolveAudience(request.Resource, null, client, idpOptions.Issuer),
            subject: principal.Id,
            azp: serviceAccount.Key,
            scope: finalScope,
            roles: roles,
            ttlSeconds: client.Session.AccessTokenTtlSeconds,
            principalType: PrincipalType.Machine.ToString(),
            actor: client.IsolationId.ToString(),
            sessionId: null);

        return new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            IdToken = null,
            RefreshToken = null,
            Scope = finalScope
        };
    }

    private static string MergeScopes(IReadOnlyList<string> clientScopes, IReadOnlyList<string> principalScopes)
    {
        var allowed = clientScopes.ToHashSet(StringComparer.Ordinal);
        return string.Join(' ', principalScopes.Where(allowed.Contains));
    }

    private static string ApplyRequestedScope(string grantedScope, string? requestedScope)
    {
        if (string.IsNullOrWhiteSpace(requestedScope))
            return grantedScope;

        var granted = grantedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var requested = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (requested.Any(s => !granted.Contains(s)))
            throw new BusinessException(IdpMessageKeys.UpscopingNotAllowed);

        return string.Join(' ', requested);
    }
}