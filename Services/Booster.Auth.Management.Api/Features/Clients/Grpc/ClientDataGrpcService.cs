using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Grpc;
using Booster.Database.Repositories;
using Booster.Hashing.TwoWays.Aes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.Clients.Grpc;

[AllowAnonymous]
public sealed class ClientDataGrpcService(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<IdentityProvider, AuthNDbContext> identityProviderRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    ISecretProtector secretProtector)
    : ClientDataService.ClientDataServiceBase
{
    public override async Task<GetPoolDefinitionsResponse> GetPoolDefinitions(
        GetPoolDefinitionsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserPoolId, out var poolId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_pool_id geçersiz."));

        var ct = context.CancellationToken;

        if (!await userPoolRepository.AnyAsync(p => p.Id == poolId, cancellationToken: ct))
            return new GetPoolDefinitionsResponse { Found = false };

        var roles = await roleRepository.Query()
            .AsNoTracking()
            .Where(r => r.UserPoolId == poolId)
            .Select(r => new PoolRoleMessage { Id = r.Id.ToString(), Key = r.Key })
            .ToListAsync(ct);

        var scopes = await scopeDefinitionRepository.Query()
            .AsNoTracking()
            .Where(s => s.UserPoolId == poolId)
            .Select(s => s.Scope)
            .ToListAsync(ct);

        var response = new GetPoolDefinitionsResponse { Found = true };
        response.Roles.AddRange(roles);
        response.Scopes.AddRange(scopes);

        return response;
    }

    public override async Task<GetClientDataResponse> GetPoolData(
        GetPoolDefinitionsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserPoolId, out var poolId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_pool_id geçersiz."));

        var ct = context.CancellationToken;

        var pool = await userPoolRepository.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == poolId, ct);
        if (pool is null)
            return new GetClientDataResponse { Found = false };

        // Istemciye ozel alanlar bos: bu veri yalnizca havuz politikasi icin kullanilir (OAuth akisi YOK).
        var poolOnly = new Client
        {
            Id = Guid.Empty,
            ClientKey = string.Empty,
            DisplayName = pool.Name,
            UserPoolId = pool.Id,
            UserPool = pool,
            Status = EntityStatus.Active,
            AllowedGrantTypes = OAuthGrantType.None,
            RequireConsent = false
        };

        var defaultRoles = await ResolveDefaultRolesAsync(pool, ct);
        var roleScopes = await ResolveRoleScopesAsync(pool.Id, ct);

        return new GetClientDataResponse
        {
            Found = true,
            Client = ClientDataMapper.ToMessage(BuildClientData(poolOnly, defaultRoles, [], roleScopes))
        };
    }

    public override async Task<GetClientDataResponse> GetByClientKey(
        GetClientDataRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ClientKey))
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "client_key boş olamaz."));

        var ct = context.CancellationToken;

        var client = await clientRepository.Query()
            .AsNoTracking()
            .Include(c => c.UserPool)
            .Include(c => c.AllowedScopes)
            .FirstOrDefaultAsync(c => c.ClientKey == request.ClientKey, ct);

        if (client is null)
            return new GetClientDataResponse { Found = false };

        var defaultRoles = await ResolveDefaultRolesAsync(client.UserPool, ct);
        var federationProviders = await ResolveFederationProvidersAsync(client.UserPool.Id, ct);
        var roleScopes = await ResolveRoleScopesAsync(client.UserPool.Id, ct);

        return new GetClientDataResponse
        {
            Found = true,
            Client = ClientDataMapper.ToMessage(BuildClientData(client, defaultRoles, federationProviders, roleScopes))
        };
    }

    private async Task<List<DefaultRoleData>> ResolveDefaultRolesAsync(UserPool pool, CancellationToken ct)
    {
        if (pool.DefaultRoleIds.Count == 0)
            return [];

        return await roleRepository.Query()
            .AsNoTracking()
            .Where(r => pool.DefaultRoleIds.Contains(r.Id))
            .Select(r => new DefaultRoleData { RoleId = r.Id, RoleName = r.Key })
            .ToListAsync(ct);
    }

    /// <summary>Havuz rollerinin kapsamlari (yalnizca kapsami olan roller). Rol kaydedilince havuz onbellegi temizlenir.</summary>
    private async Task<List<RoleScopesData>> ResolveRoleScopesAsync(Guid userPoolId, CancellationToken ct)
    {
        var rows = await roleRepository.Query()
            .AsNoTracking()
            .Where(r => r.UserPoolId == userPoolId && r.RoleScopes.Any())
            .Select(r => new { r.Id, Scopes = r.RoleScopes.Select(s => s.Scope).ToList() })
            .ToListAsync(ct);
        return rows.Select(r => new RoleScopesData { RoleId = r.Id, Scopes = r.Scopes }).ToList();
    }

    private async Task<List<FederationProviderData>> ResolveFederationProvidersAsync(Guid userPoolId, CancellationToken ct)
        => await identityProviderRepository.Query()
            .AsNoTracking()
            .Where(p => p.UserPoolId == userPoolId && p.Status == EntityStatus.Active)
            .OrderBy(p => p.DisplayName)
            .Select(p => new FederationProviderData
            {
                Id = p.Id,
                Key = p.Key,
                ProviderType = p.ProviderType,
                DisplayName = p.DisplayName
            })
            .ToListAsync(ct);

    public override async Task<GetFederationProviderResponse> GetFederationProvider(
        GetFederationProviderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserPoolId, out var userPoolId) || string.IsNullOrWhiteSpace(request.Key))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_pool_id ve key zorunludur."));

        var provider = await identityProviderRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserPoolId == userPoolId && p.Key == request.Key, context.CancellationToken);

        if (provider is null)
            return new GetFederationProviderResponse { Found = false };

        var message = new FederationProviderConfigMessage
        {
            Id = provider.Id.ToString(),
            Key = provider.Key,
            ProviderType = provider.ProviderType,
            DisplayName = provider.DisplayName,
            IsActive = provider.Status == EntityStatus.Active
        };

        foreach (var (name, value) in provider.Settings)
            message.Settings[name] = value;

        if (!string.IsNullOrEmpty(provider.ClientSecretEncrypted)
            && secretProtector.Unprotect(provider.ClientSecretEncrypted) is { } secret)
        {
            message.Settings[ClientSecretSetting] = secret;
        }

        return new GetFederationProviderResponse { Found = true, Provider = message };
    }

    private const string ClientSecretSetting = "client_secret";

    private static ClientData BuildClientData(
        Client client, List<DefaultRoleData> defaultRoles, List<FederationProviderData> federationProviders,
        List<RoleScopesData> roleScopes)
    {
        var pool = client.UserPool;
        var policy = pool.Policy;

        return new ClientData
        {
            ClientId = client.Id,
            ClientKey = client.ClientKey,
            DisplayName = client.DisplayName,
            AllowedGrantTypes = client.AllowedGrantTypes,
            AllowedScopes = [.. client.AllowedScopes.Select(s => s.Scope)],

            IsolationId = pool.Id,

            IsActive = client.Status == EntityStatus.Active && pool.Status == EntityStatus.Active,

            RedirectUris = [.. client.RedirectUris],
            PostLogoutRedirectUris = [.. client.PostLogoutRedirectUris],
            AllowedAudiences = [.. client.AllowedAudiences],
            RequireConsent = client.RequireConsent,

            Login = new LoginPolicyData
            {
                AllowedLoginProviders = policy.Login.AllowedLoginProviders,
                AllowedIdentifiers = [.. policy.Login.AllowedIdentifiers],
                MaxFailedAttempts = policy.Login.MaxFailedAttempts,
                LockoutSeconds = policy.Login.LockoutSeconds,
                DefaultCountryCode = policy.Login.DefaultCountryCode
            },
            Registration = new RegistrationPolicyData
            {
                Mode = policy.Registration.Mode,
                RequireEmailVerification = policy.Registration.RequireEmailVerification,
                RequirePhoneVerification = policy.Registration.RequirePhoneVerification,
                AllowFederatedJit = policy.Registration.AllowFederatedJit,
                AllowSelfDeletion = policy.Registration.AllowSelfDeletion
            },
            Password = new PasswordPolicyData
            {
                MinLength = policy.Password.MinLength,
                RequireUppercase = policy.Password.RequireUppercase,
                RequireLowercase = policy.Password.RequireLowercase,
                RequireDigit = policy.Password.RequireDigit,
                RequireNonAlphanumeric = policy.Password.RequireNonAlphanumeric,
                HistoryCount = policy.Password.HistoryCount,
                ExpiryDays = policy.Password.ExpiryDays
            },
            Session = new SessionPolicyData
            {
                AccessTokenTtlSeconds = policy.Session.AccessTokenTtlSeconds,
                RefreshTokenTtlSeconds = policy.Session.RefreshTokenTtlSeconds,
                IdleTimeoutSeconds = policy.Session.IdleTimeoutSeconds
            },
            Mfa = new MfaPolicyData
            {
                Required = policy.Mfa.Required,
                AllowedMethods = policy.Mfa.AllowedMethods
            },
            DefaultScopes = [.. pool.DefaultScopes],
            DefaultRoles = defaultRoles,
            RoleScopes = roleScopes,
            FederationProviders = federationProviders
        };
    }
}
