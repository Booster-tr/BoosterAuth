using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;

namespace Booster.Auth.Management.Api.Bootstrappers;


public sealed class ManagementBootstrapper(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAsyncRepository<ClientAllowedScope, AuthNDbContext> clientAllowedScopeRepository,
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    IClientDataCacheStore clientDataCacheStore,
    BootstrapOptions bootstrapOptions)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var alreadySeeded = await tenantRepository.AnyAsync(
            t => t.Id == AuthConstants.ManagementTenantId, cancellationToken: ct);

        if (alreadySeeded)
        {
            await SyncManagementClientAsync(ct);
            return;
        }

       
        var tenant = new Tenant
        {
            Id = AuthConstants.ManagementTenantId,
            Name = AuthConstants.ManagementTenantName,
            OwnerId = AuthConstants.AdminUserId
        };

        var pool = new UserPool
        {
            Id = AuthConstants.ManagementPoolId,
            TenantId = tenant.Id,
            Name = AuthConstants.ManagementPoolName,
            Status = EntityStatus.Active,
            Policy = BuildManagementPolicy(),
            DefaultScopes = [AuthConstants.ManagementPanelScope]
        };

        var panelScope = new ScopeDefinition
        {
            Id = EntityId.New(),
            UserPoolId = pool.Id,
            Scope = AuthConstants.ManagementPanelScope,
            DisplayName = "Yönetim paneli erişimi"
        };

        var adminRole = new Role
        {
            Id = AuthConstants.AdminRoleId,
            UserPoolId = pool.Id,
            Key = AuthConstants.AdminRoleName,
            DisplayName = AuthConstants.AdminRoleDisplayName
        };

        var adminRoleScope = new RoleScope
        {
            Id = EntityId.New(),
            RoleId = adminRole.Id,
            Scope = panelScope.Scope
        };

        var client = new Client
        {
            Id = AuthConstants.ManagementClientId,
            UserPoolId = pool.Id,
            ClientKey = AuthConstants.ManagementClientKey,
            DisplayName = AuthConstants.ManagementClientName,
            Status = EntityStatus.Active,

            AllowedGrantTypes = OAuthGrantType.AuthorizationCode | OAuthGrantType.RefreshToken
                              | OAuthGrantType.ClientCredentials | OAuthGrantType.TokenExchange,
            RedirectUris = [bootstrapOptions.ManagementClientRedirectUri],
            PostLogoutRedirectUris = [bootstrapOptions.ManagementClientPostLogoutRedirectUri],
            AllowedAudiences =
            [
                bootstrapOptions.ManagementClientAudience,
                bootstrapOptions.IdpAudience,
                bootstrapOptions.AuthZAudience
            ],
            RequireConsent = false
        };

        var clientScope = new ClientAllowedScope
        {
            Id = EntityId.New(),
            ClientId = client.Id,
            Scope = panelScope.Scope
        };

        await tenantRepository.AddAsync(tenant, ct);
        await userPoolRepository.AddAsync(pool, ct);
        await scopeDefinitionRepository.AddAsync(panelScope, ct);
        await roleRepository.AddAsync(adminRole, ct);
        await roleScopeRepository.AddAsync(adminRoleScope, ct);
        await clientRepository.AddAsync(client, ct);
        await clientAllowedScopeRepository.AddAsync(clientScope, ct);

        await tenantRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Kurulum sonrası ortam ayarı değişirse (ör. panel/issuer adresi) panel client'ı
    /// yeni adreslerle açılabilsin diye yapılandırılan değerler EKLENİR. Panelden elle
    /// eklenmiş adresler silinmez.
    /// </summary>
    private async Task SyncManagementClientAsync(CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == AuthConstants.ManagementClientId,
            withoutGlobalFilter: true,
            cancellationToken: ct);

        if (client is null)
            return;

        var redirectUris = WithMissing(client.RedirectUris, bootstrapOptions.ManagementClientRedirectUri);
        var postLogoutUris = WithMissing(client.PostLogoutRedirectUris, bootstrapOptions.ManagementClientPostLogoutRedirectUri);
        var audiences = WithMissing(client.AllowedAudiences,
            bootstrapOptions.ManagementClientAudience,
            bootstrapOptions.IdpAudience,
            bootstrapOptions.AuthZAudience);

        if (redirectUris is null && postLogoutUris is null && audiences is null)
            return;

        // Yeni liste örneği atanır: koleksiyon yerinde değişirse EF değişikliği
        // (karşılaştırıcısız JSON/dizi kolonlarında) fark etmeyebilir.
        if (redirectUris is not null) client.RedirectUris = redirectUris;
        if (postLogoutUris is not null) client.PostLogoutRedirectUris = postLogoutUris;
        if (audiences is not null) client.AllowedAudiences = audiences;

        await clientRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidateClientAsync(client.ClientKey, ct);
    }

    /// <summary>Eksik değer yoksa null, varsa eklenmiş YENİ liste döner.</summary>
    private static List<string>? WithMissing(List<string> current, params string?[] values)
    {
        var missing = values
            .Where(v => !string.IsNullOrWhiteSpace(v) && !current.Contains(v!))
            .Distinct()
            .ToList();

        return missing.Count == 0 ? null : [.. current, .. missing!];
    }

    private static UserPoolPolicy BuildManagementPolicy()
        => new()
        {
            Login = new LoginPolicy
            {
                AllowedLoginProviders = LoginProviderType.Local | LoginProviderType.Federation,
                AllowedIdentifiers = [IdentifierType.Email, IdentifierType.Username],
                MaxFailedAttempts = 5,
                LockoutSeconds = 900,
                DefaultCountryCode = "90"
            },
            Password = new PasswordPolicy
            {
                MinLength = 8,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigit = true,
                RequireNonAlphanumeric = true,
                HistoryCount = 3,
                ExpiryDays = 0
            },
            Mfa = new MfaPolicy
            {
                Required = false,
                AllowedMethods = MfaMethod.Email | MfaMethod.Sms
            },
            Registration = new RegistrationPolicy
            {
                Mode = RegistrationMode.SelfSignup,
                RequireEmailVerification = true,
                RequirePhoneVerification = false,
                AllowFederatedJit = false
            },
            Session = new SessionPolicy
            {
                AccessTokenTtlSeconds = 900,          // 15 dk
                RefreshTokenTtlSeconds = 1_209_600,   // 14 gün
                IdleTimeoutSeconds = 0
            }
        };
}
