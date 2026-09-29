using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Grpc;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.FederationStores;
using Booster.Auth.Idp.Stores.FederationStores.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Login.FederationLogin;

public sealed class CompleteFederationLoginHandler(
    ISecureCacheStore<FederationStateData> stateStore,
    IClientDataStore clientDataStore,
    IFederationProviderConfigStore providerConfigStore,
    IProviderFactory providerFactory,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<PrincipalIdentity, IdpDbContext> identityRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    ISessionStore sessionStore,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<CompleteFederationLoginCommand, CompleteFederationLoginResult>
{
    public async Task<CompleteFederationLoginResult> ExecuteAsync(
        CompleteFederationLoginCommand cmd, CancellationToken ct)
    {
        var state = await stateStore.ConsumeAsync(cmd.State, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.FederationStateInvalid);

        var client = await clientDataStore.GetByClientKeyAsync(state.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var config = await providerConfigStore.GetAsync(state.IsolationId, state.ProviderKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.FederationProviderNotFound);

        if (!config.IsActive || !client.Login.AllowedLoginProviders.HasFlag(LoginProviderType.Federation))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        var provider = providerFactory.GetFederationProvider(config.ProviderType)
            ?? throw new AuthorizeException(IdpMessageKeys.ProviderNotAvailable);

        var result = await provider.HandleCallbackAsync(
            new FederationCallback
            {
                Parameters = cmd.Parameters,
                RedirectUri = state.RedirectUri,
                CodeVerifier = state.CodeVerifier,
                ExpectedNonce = state.Nonce
            },
            config.ToProviderConfig(state.IsolationId),
            ct);

        if (!result.Succeeded)
            throw new AuthorizeException(MapFailure(result.FailureReason), state.InteractionKey);

        var external = result.Identity!;

        using var scope = requestContextAccessor.BeginSystemContext(state.IsolationId);

        var existing = await identityRepository.Query()
            .FirstOrDefaultAsync(
                i => i.IdentityProviderId == state.IdentityProviderId
                  && i.ExternalSubject == external.Subject,
                ct);

        if (existing is not null)
            return await SignInExistingAsync(existing, state, client, cmd, ct);

        var email = external.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(email))
            throw new AuthorizeException(IdpMessageKeys.FederationEmailMissing, state.InteractionKey);

        if (!external.EmailVerified)
            throw new AuthorizeException(IdpMessageKeys.FederationEmailNotVerified, state.InteractionKey);

        var localPrincipal = await principalRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == email, ct);

        if (localPrincipal is not null)
        {
            return new CompleteFederationLoginResult
            {
                Outcome = FederationOutcome.LocalAccountExists,
                InteractionKey = state.InteractionKey,
                Email = email
            };
        }

        if (!client.Registration.AllowFederatedJit)
            throw new AuthorizeException(IdpMessageKeys.FederationJitDisabled, state.InteractionKey);

        return await ProvisionAndSignInAsync(external, email, state, client, cmd, ct);
    }

    private async Task<CompleteFederationLoginResult> SignInExistingAsync(
        PrincipalIdentity identity,
        FederationStateData state,
        ClientData client,
        CompleteFederationLoginCommand cmd,
        CancellationToken ct)
    {
        var principal = await principalRepository.GetAsync(
            p => p.Id == identity.PrincipalId, enableTracking: false, cancellationToken: ct);

        if (principal is null || principal.Status != PrincipalStatus.Active)
            throw new AuthorizeException(IdpMessageKeys.FederationAccountInactive, state.InteractionKey);

        identity.LastLoginAt = DateTimeOffset.UtcNow;
        identityRepository.Update(identity);
        await identityRepository.SaveChangesAsync(ct);

        return await OpenSessionAsync(principal.Id, state, client, cmd, ct);
    }

    private async Task<CompleteFederationLoginResult> ProvisionAndSignInAsync(
        ExternalIdentity external,
        string email,
        FederationStateData state,
        ClientData client,
        CompleteFederationLoginCommand cmd,
        CancellationToken ct)
    {
        var principal = new Principal
        {
            Id = EntityId.New(),
            DisplayName = external.DisplayName ?? email,
            Email = email,
            EmailVerified = true,
            Type = PrincipalType.User,
            Status = PrincipalStatus.Active,
            CreatedTime = DateTimeOffset.UtcNow
        };

        var identity = new PrincipalIdentity
        {
            Id = EntityId.New(),
            PrincipalId = principal.Id,
            IdentityProviderId = state.IdentityProviderId,
            IsolationId = state.IsolationId,
            ExternalSubject = external.Subject,
            LastLoginAt = DateTimeOffset.UtcNow,
            CreatedTime = DateTimeOffset.UtcNow
        };

        await principalRepository.AddAsync(principal, ct);
        await identityRepository.AddAsync(identity, ct);
        await AssignDefaultsAsync(principal.Id, client, ct);

        var endUser = new WriteRelationsRequest { IsolationId = AuthConstants.ManagementPoolId.ToString() };
        endUser.Tuples.Add(new RelationTuple
        {
            User = AuthConstants.User(principal.Id),
            Relation = AuthConstants.EndUserRelation,
            Object = AuthConstants.Pool(state.IsolationId)
        });
        await authorizationClient.WriteRelationsAsync(endUser, cancellationToken: ct);

        await principalRepository.SaveChangesAsync(ct);

        return await OpenSessionAsync(principal.Id, state, client, cmd, ct);
    }

    private async Task<CompleteFederationLoginResult> OpenSessionAsync(
        Guid principalId,
        FederationStateData state,
        ClientData client,
        CompleteFederationLoginCommand cmd,
        CancellationToken ct)
    {
        var (session, token) = await sessionStore.CreateAsync(
            principalId: principalId,
            clientId: client.ClientId,
            ttl: TimeSpan.FromSeconds(client.Session.RefreshTokenTtlSeconds),
            deviceName: null,
            userAgent: cmd.UserAgent,
            ipAddress: cmd.IpAddress,
            authMethod: state.ProviderKey,
            ct: ct);

        return new CompleteFederationLoginResult
        {
            Outcome = FederationOutcome.SignedIn,
            InteractionKey = state.InteractionKey,
            SessionToken = token,
            ExpiresAt = session.ExpiresAt
        };
    }

    private async Task AssignDefaultsAsync(Guid principalId, ClientData client, CancellationToken ct)
    {
        foreach (var role in client.DefaultRoles)
        {
            await principalRoleRepository.AddAsync(new PrincipalRole
            {
                Id = EntityId.New(),
                PrincipalId = principalId,
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                CreatedTime = DateTimeOffset.UtcNow
            }, ct);
        }

        foreach (var scopeName in client.DefaultScopes)
        {
            await principalScopeRepository.AddAsync(new PrincipalScope
            {
                Id = EntityId.New(),
                PrincipalId = principalId,
                Scope = scopeName,
                CreatedTime = DateTimeOffset.UtcNow
            }, ct);
        }
    }

    private static string MapFailure(string? reason) => reason switch
    {
        FederationFailures.AuthorizationDenied => IdpMessageKeys.FederationCancelled,
        FederationFailures.ProviderInactive => IdpMessageKeys.ProviderNotAllowed,
        FederationFailures.ProviderMisconfigured => IdpMessageKeys.FederationProviderNotFound,
        _ => IdpMessageKeys.FederationFailed
    };
}

public enum FederationOutcome
{
    SignedIn = 0,
    LocalAccountExists = 1
}

public sealed class CompleteFederationLoginCommand : ICommand<CompleteFederationLoginResult>
{
    public string State { get; set; } = default!;

    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class CompleteFederationLoginResult
{
    public required FederationOutcome Outcome { get; init; }

    public required string InteractionKey { get; init; }

    public string? SessionToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public string? Email { get; init; }
}
