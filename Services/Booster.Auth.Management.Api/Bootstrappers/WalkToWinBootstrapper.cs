using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;

namespace Booster.Auth.Management.Api.Bootstrappers;

/// <summary>
/// WalkToWin mobil uygulamasının kimlik altyapısını ilk kez kurar:
/// tenant → oyuncu havuzu (yalnızca e-posta) → public mobil client.
/// <para>
/// ManagementBootstrapper ile aynı desen: sabit kimlikler, idempotent. Kurulumdan sonra
/// her şey arayüzden yönetilir; tek istisna, ayarlarda olup client'ta eksik olan
/// yönlendirme adreslerinin eklenmesidir (geliştirme ortamında Expo adresleri için).
/// Mevcut adresler hiçbir zaman silinmez.
/// </para>
/// AuthZ tarafındaki owner/parent ilişkileri Booster.Auth.Z/WalkToWinBootstrapper ile yazılır.
/// </summary>
public sealed class WalkToWinBootstrapper(
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
    private WalkToWinBootstrapOptions Options => bootstrapOptions.WalkToWin;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!Options.Enabled)
            return;

        var alreadySeeded = await tenantRepository.AnyAsync(
            t => t.Id == AuthConstants.WalkToWinTenantId, cancellationToken: ct);

        if (alreadySeeded)
        {
            await SyncClientUrisAsync(ct);
            return;
        }

        var tenant = new Tenant
        {
            Id = AuthConstants.WalkToWinTenantId,
            Name = AuthConstants.WalkToWinTenantName,
            OwnerId = AuthConstants.AdminUserId
        };

        var pool = new UserPool
        {
            Id = AuthConstants.WalkToWinPoolId,
            TenantId = tenant.Id,
            Name = AuthConstants.WalkToWinPoolName,
            Status = EntityStatus.Active,
            Policy = BuildPlayerPolicy(),
            DefaultScopes = [AuthConstants.WalkToWinPlayScope],
            DefaultRoleIds = [AuthConstants.WalkToWinPlayerRoleId]
        };

        var playScope = new ScopeDefinition
        {
            Id = EntityId.New(),
            UserPoolId = pool.Id,
            Scope = AuthConstants.WalkToWinPlayScope,
            DisplayName = "Av başlatma ve oynama",
            Description = "Mobil uygulamada etkinliklere katılma, durak tarama ve görev yanıtlama."
        };

        var playerRole = new Role
        {
            Id = AuthConstants.WalkToWinPlayerRoleId,
            UserPoolId = pool.Id,
            Key = AuthConstants.WalkToWinPlayerRoleName,
            DisplayName = AuthConstants.WalkToWinPlayerRoleDisplayName,
            Description = "Kendi kendine kayıt olan her kullanıcıya varsayılan olarak atanır."
        };

        var playerRoleScope = new RoleScope
        {
            Id = EntityId.New(),
            RoleId = playerRole.Id,
            Scope = playScope.Scope
        };

        var client = new Client
        {
            Id = AuthConstants.WalkToWinClientId,
            UserPoolId = pool.Id,
            ClientKey = AuthConstants.WalkToWinClientKey,
            DisplayName = AuthConstants.WalkToWinClientName,
            Status = EntityStatus.Active,

            // Public mobil client: yalnızca PKCE'li authorization code + refresh token.
            // Client credentials / token exchange bilinçli olarak kapalı (cihazda secret tutulamaz).
            AllowedGrantTypes = OAuthGrantType.AuthorizationCode | OAuthGrantType.RefreshToken,
            RedirectUris = Distinct(Options.RedirectUris),
            PostLogoutRedirectUris = Distinct(Options.PostLogoutRedirectUris),
            AllowedAudiences = Distinct([Options.ApiAudience, bootstrapOptions.IdpAudience]),

            // Birinci taraf uygulama: kullanıcıya ayrıca onay ekranı gösterilmez.
            RequireConsent = false
        };

        var clientScope = new ClientAllowedScope
        {
            Id = EntityId.New(),
            ClientId = client.Id,
            Scope = playScope.Scope
        };

        await tenantRepository.AddAsync(tenant, ct);
        await userPoolRepository.AddAsync(pool, ct);
        await scopeDefinitionRepository.AddAsync(playScope, ct);
        await roleRepository.AddAsync(playerRole, ct);
        await roleScopeRepository.AddAsync(playerRoleScope, ct);
        await clientRepository.AddAsync(client, ct);
        await clientAllowedScopeRepository.AddAsync(clientScope, ct);

        await tenantRepository.SaveChangesAsync(ct);
    }

    /// <summary>Ayarlarda tanımlı olup client'ta bulunmayan adresleri ekler; hiçbir adresi silmez.</summary>
    private async Task SyncClientUrisAsync(CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == AuthConstants.WalkToWinClientId, enableTracking: true, cancellationToken: ct);

        if (client is null)
            return;

        var redirectAdded = AddMissing(client.RedirectUris, Options.RedirectUris);
        var logoutAdded = AddMissing(client.PostLogoutRedirectUris, Options.PostLogoutRedirectUris);

        if (!redirectAdded && !logoutAdded)
            return;

        clientRepository.Update(client);
        await clientRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidateClientAsync(client.ClientKey, ct);
    }

    private static bool AddMissing(List<string> target, IEnumerable<string> desired)
    {
        var missing = Distinct(desired).Where(uri => !target.Contains(uri, StringComparer.Ordinal)).ToList();
        target.AddRange(missing);
        return missing.Count > 0;
    }

    private static List<string> Distinct(IEnumerable<string?> values)
        => values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Oyuncu havuzu politikası: tüm işlemler e-posta adresi üzerinden yürür.
    /// - Tanımlayıcı yalnızca e-posta (kullanıcı adı / telefon kapalı)
    /// - Giriş: e-posta + şifre veya e-postaya gelen tek kullanımlık kod
    /// - Kayıt: self-signup, e-posta doğrulaması zorunlu
    /// - MFA: yalnızca e-posta kanalı
    /// - Federation (Google, Apple...) kapalı
    /// </summary>
    private static UserPoolPolicy BuildPlayerPolicy()
        => new()
        {
            Login = new LoginPolicy
            {
                AllowedLoginProviders = LoginProviderType.Local | LoginProviderType.EmailCode,
                AllowedIdentifiers = [IdentifierType.Email],
                MaxFailedAttempts = 5,
                LockoutSeconds = 900
            },
            Password = new PasswordPolicy
            {
                MinLength = 8,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigit = true,
                RequireNonAlphanumeric = false,
                HistoryCount = 3,
                ExpiryDays = 0
            },
            Mfa = new MfaPolicy
            {
                Required = false,
                AllowedMethods = MfaMethod.Email
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
                AccessTokenTtlSeconds = 900,          // 15 dk — cihazda yalnızca bellekte tutulur
                RefreshTokenTtlSeconds = 2_592_000,   // 30 gün — mobilde uzun oturum, rotation ile
                IdleTimeoutSeconds = 0
            }
        };
}
