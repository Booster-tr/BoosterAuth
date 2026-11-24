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
<<<<<<< Updated upstream:Services/Booster.Auth.Management.Api/Bootstrappers/LicensingBootstrapper.cs
/// Lisans portalinin kimlik altyapisini ilk kez kurar:
/// tenant -> musteri havuzu (yalnizca e-posta, self-signup) -> portal client'i (PKCE, secret yok).
/// <para>
/// YALNIZCA <c>BootstrapOptions.Licensing.Enabled</c> true ise calisir (Booster'in kendi kurulumu).
/// Musteri on-prem kurulumlarinda varsayilan kapali oldugu icin hicbir sey olusmaz.
/// </para>
/// ManagementBootstrapper ile ayni desen: sabit kimlikler, idempotent. Ayarlarda olup client'ta eksik olan
/// yonlendirme adresleri eklenir, mevcutlar silinmez.
/// AuthZ tarafindaki owner/parent iliskileri Booster.Auth.Z/LicensingBootstrapper ile yazilir.
=======
/// WalkToWin mobil uygulamasının kimlik altyapısını kurar:
/// tenant → oyuncu havuzu (yalnızca e-posta) → public mobil client, ve havuzun yetki modeli
/// (scope tanımları + hunter / hunt_creator / admin rolleri).
/// <para>
/// ManagementBootstrapper ile aynı desen: sabit kimlikler, idempotent. Kurulumdan sonra
/// her şey arayüzden yönetilir; açılışta yalnızca EKSİKLER eklenir (yönlendirme adresleri,
/// scope tanımları, roller, rol scope'ları). Hiçbir kayıt silinmez.
/// </para>
/// AuthZ tarafındaki ilişkiler ve havuzun kendi şeması Booster.Auth.Z/WalkToWinBootstrapper ile yazılır.
>>>>>>> Stashed changes:Services/Booster.Auth.Management.Api/Bootstrappers/WalkToWinBootstrapper.cs
/// </summary>
public sealed class LicensingBootstrapper(
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
    private LicensingBootstrapOptions Options => bootstrapOptions.Licensing;

    private static readonly (string Scope, string DisplayName, string Description)[] Scopes =
    [
        (AuthConstants.WalkToWinPlayScope, "Av başlatma ve oynama",
            "Mobil uygulamada etkinliklere katılma, durak tarama ve görev yanıtlama."),
        (AuthConstants.WalkToWinHuntsManageScope, "Av yönetimi",
            "Av oluşturma, durak ve görev düzenleme, yayınlama. Client'a eklenmez; rol atanırken kullanıcıya verilir."),
        (AuthConstants.WalkToWinAdminScope, "WalkToWin yönetimi",
            "Tüm avları yönetme. Client'a eklenmez; rol atanırken kullanıcıya verilir.")
    ];

    private static readonly (Guid Id, string Key, string DisplayName, string Description, string[] Scopes)[] Roles =
    [
        (AuthConstants.WalkToWinHunterRoleId, AuthConstants.WalkToWinHunterRoleName, AuthConstants.WalkToWinHunterRoleDisplayName,
            "Kendi kendine kayıt olan her kullanıcıya varsayılan olarak atanır.",
            [AuthConstants.WalkToWinPlayScope]),
        (AuthConstants.WalkToWinHuntCreatorRoleId, AuthConstants.WalkToWinHuntCreatorRoleName, AuthConstants.WalkToWinHuntCreatorRoleDisplayName,
            "Kendi avlarını oluşturur ve yönetir.",
            [AuthConstants.WalkToWinPlayScope, AuthConstants.WalkToWinHuntsManageScope]),
        (AuthConstants.WalkToWinAdminRoleId, AuthConstants.WalkToWinAdminRoleName, AuthConstants.WalkToWinAdminRoleDisplayName,
            "Tüm avları yönetir.",
            [AuthConstants.WalkToWinPlayScope, AuthConstants.WalkToWinHuntsManageScope, AuthConstants.WalkToWinAdminScope])
    ];

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!Options.Enabled)
            return;

        var alreadySeeded = await tenantRepository.AnyAsync(
            t => t.Id == AuthConstants.LicensingTenantId, cancellationToken: ct);

        if (!alreadySeeded)
            await SeedAsync(ct);
        else
            await SyncClientUrisAsync(ct);

        await SyncAccessModelAsync(ct);
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var tenant = new Tenant
        {
            Id = AuthConstants.LicensingTenantId,
            Name = AuthConstants.LicensingTenantName,
            OwnerId = AuthConstants.AdminUserId
        };

        var pool = new UserPool
        {
            Id = AuthConstants.LicensingPoolId,
            TenantId = tenant.Id,
            Name = AuthConstants.LicensingPoolName,
            Status = EntityStatus.Active,
<<<<<<< Updated upstream:Services/Booster.Auth.Management.Api/Bootstrappers/LicensingBootstrapper.cs
            Policy = BuildCustomerPolicy(),
            DefaultScopes = [AuthConstants.LicensingPortalScope],
            DefaultRoleIds = [AuthConstants.LicensingCustomerRoleId]
        };

        var portalScope = new ScopeDefinition
        {
            Id = EntityId.New(),
            UserPoolId = pool.Id,
            Scope = AuthConstants.LicensingPortalScope,
            DisplayName = "Lisans portali",
            Description = "Kurulum ekleme, deneme lisansi alma, siparis verme ve lisanslari goruntuleme."
        };

        var customerRole = new Role
        {
            Id = AuthConstants.LicensingCustomerRoleId,
            UserPoolId = pool.Id,
            Key = AuthConstants.LicensingCustomerRoleName,
            DisplayName = AuthConstants.LicensingCustomerRoleDisplayName,
            Description = "Portala kendi kendine kayit olan her musteriye varsayilan olarak atanir."
        };

        var customerRoleScope = new RoleScope
        {
            Id = EntityId.New(),
            RoleId = customerRole.Id,
            Scope = portalScope.Scope
=======
            Policy = BuildPlayerPolicy(),
            DefaultScopes = [AuthConstants.WalkToWinPlayScope],
            DefaultRoleIds = [AuthConstants.WalkToWinHunterRoleId]
>>>>>>> Stashed changes:Services/Booster.Auth.Management.Api/Bootstrappers/WalkToWinBootstrapper.cs
        };

        var client = new Client
        {
            Id = AuthConstants.LicensingClientId,
            UserPoolId = pool.Id,
            ClientKey = AuthConstants.LicensingClientKey,
            DisplayName = AuthConstants.LicensingClientName,
            Status = EntityStatus.Active,

            // Yonetim paneli gibi: PKCE'li authorization code, client secret YOK.
            // Portal yalnizca kimligi (id_token) kullanir, baska bir API cagirmaz.
            AllowedGrantTypes = OAuthGrantType.AuthorizationCode | OAuthGrantType.RefreshToken,
            RedirectUris = Distinct(Options.RedirectUris),
            PostLogoutRedirectUris = Distinct(Options.PostLogoutRedirectUris),
            AllowedAudiences = Distinct([bootstrapOptions.IdpAudience]),

            // Birinci taraf uygulama: musteriye ayrica onay ekrani gosterilmez.
            RequireConsent = false
        };

        // Client'a YALNIZCA oynama scope'u izinlidir; yönetim scope'ları için bkz. AuthConstants.WalkToWinHuntsManageScope.
        var clientScope = new ClientAllowedScope
        {
            Id = EntityId.New(),
            ClientId = client.Id,
<<<<<<< Updated upstream:Services/Booster.Auth.Management.Api/Bootstrappers/LicensingBootstrapper.cs
            Scope = portalScope.Scope
=======
            Scope = AuthConstants.WalkToWinPlayScope
>>>>>>> Stashed changes:Services/Booster.Auth.Management.Api/Bootstrappers/WalkToWinBootstrapper.cs
        };

        await tenantRepository.AddAsync(tenant, ct);
        await userPoolRepository.AddAsync(pool, ct);
<<<<<<< Updated upstream:Services/Booster.Auth.Management.Api/Bootstrappers/LicensingBootstrapper.cs
        await scopeDefinitionRepository.AddAsync(portalScope, ct);
        await roleRepository.AddAsync(customerRole, ct);
        await roleScopeRepository.AddAsync(customerRoleScope, ct);
=======
>>>>>>> Stashed changes:Services/Booster.Auth.Management.Api/Bootstrappers/WalkToWinBootstrapper.cs
        await clientRepository.AddAsync(client, ct);
        await clientAllowedScopeRepository.AddAsync(clientScope, ct);

        await tenantRepository.SaveChangesAsync(ct);
    }

<<<<<<< Updated upstream:Services/Booster.Auth.Management.Api/Bootstrappers/LicensingBootstrapper.cs
    /// <summary>Ayarlarda tanimli olup client'ta bulunmayan adresleri ekler; hicbir adresi silmez.</summary>
=======
    /// <summary>
    /// Scope tanımları, roller ve rol scope'larından eksik olanları ekler. İlk kurulumda "player" adıyla
    /// açılan rol "hunter" olarak yeniden adlandırılır (kimliği aynı, atamalar korunur).
    /// Panelden yapılan değişiklikler (ek scope, ad) ezilmez; yalnızca eksik olan tamamlanır.
    /// </summary>
    private async Task SyncAccessModelAsync(CancellationToken ct)
    {
        var poolId = AuthConstants.WalkToWinPoolId;
        var changed = false;

        foreach (var (scope, displayName, description) in Scopes)
        {
            var exists = await scopeDefinitionRepository.AnyAsync(
                s => s.UserPoolId == poolId && s.Scope == scope, cancellationToken: ct);
            if (exists)
                continue;

            await scopeDefinitionRepository.AddAsync(new ScopeDefinition
            {
                Id = EntityId.New(),
                UserPoolId = poolId,
                Scope = scope,
                DisplayName = displayName,
                Description = description
            }, ct);
            changed = true;
        }

        foreach (var (id, key, displayName, description, scopes) in Roles)
        {
            var role = await roleRepository.GetAsync(r => r.Id == id, enableTracking: true, cancellationToken: ct);

            if (role is null)
            {
                // Aynı anahtarla panelden açılmış bir rol varsa ona dokunulmaz; benzersiz indeks yeni kaydı reddederdi.
                var keyTaken = await roleRepository.AnyAsync(r => r.UserPoolId == poolId && r.Key == key, cancellationToken: ct);
                if (keyTaken)
                    continue;

                role = new Role { Id = id, UserPoolId = poolId, Key = key, DisplayName = displayName, Description = description };
                await roleRepository.AddAsync(role, ct);
                changed = true;
            }
            else if (role.Key == AuthConstants.WalkToWinLegacyPlayerRoleName && key == AuthConstants.WalkToWinHunterRoleName)
            {
                role.Key = key;
                role.DisplayName = displayName;
                changed = true;
            }

            foreach (var scope in scopes)
            {
                var hasScope = await roleScopeRepository.AnyAsync(s => s.RoleId == id && s.Scope == scope, cancellationToken: ct);
                if (hasScope)
                    continue;

                await roleScopeRepository.AddAsync(new RoleScope { Id = EntityId.New(), RoleId = id, Scope = scope }, ct);
                changed = true;
            }
        }

        if (!changed)
            return;

        await roleRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(poolId, ct);
    }

    /// <summary>Ayarlarda tanımlı olup client'ta bulunmayan adresleri ekler; hiçbir adresi silmez.</summary>
>>>>>>> Stashed changes:Services/Booster.Auth.Management.Api/Bootstrappers/WalkToWinBootstrapper.cs
    private async Task SyncClientUrisAsync(CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == AuthConstants.LicensingClientId, enableTracking: true, cancellationToken: ct);

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
    /// Musteri havuzu: tanimlayici yalnizca e-posta; giris sifre veya e-posta koduyla; self-signup; federasyon kapali.
    /// Deneme lisansi icin e-postanin GERCEKTEN musteriye ait olmasi gerekir. Kayit akisi bunu henuz zorlamadigi icin
    /// portal ayrica e-posta kodu ile dogrular (Faz 2.2).
    /// </summary>
    private static UserPoolPolicy BuildCustomerPolicy()
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
                AccessTokenTtlSeconds = 900,
                RefreshTokenTtlSeconds = 28_800,
                IdleTimeoutSeconds = 0
            }
        };
}
