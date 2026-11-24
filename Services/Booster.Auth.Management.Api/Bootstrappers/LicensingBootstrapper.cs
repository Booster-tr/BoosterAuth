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
/// Lisans portalinin kimlik altyapisini ilk kez kurar:
/// tenant -> musteri havuzu (yalnizca e-posta, self-signup) -> portal client'i (PKCE, secret yok).
/// <para>
/// YALNIZCA <c>BootstrapOptions.Licensing.Enabled</c> true ise calisir (Booster'in kendi kurulumu).
/// Musteri on-prem kurulumlarinda varsayilan kapali oldugu icin hicbir sey olusmaz.
/// </para>
/// ManagementBootstrapper ile ayni desen: sabit kimlikler, idempotent. Ayarlarda olup client'ta eksik olan
/// yonlendirme adresleri eklenir, mevcutlar silinmez.
/// AuthZ tarafindaki owner/parent iliskileri Booster.Auth.Z/LicensingBootstrapper ile yazilir.
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

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!Options.Enabled)
            return;

        var alreadySeeded = await tenantRepository.AnyAsync(
            t => t.Id == AuthConstants.LicensingTenantId, cancellationToken: ct);

        if (alreadySeeded)
        {
            await SyncClientUrisAsync(ct);
            return;
        }

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
            PostLogoutRedirectUris = Distinct(PostLogoutUris),
            AllowedAudiences = Distinct([bootstrapOptions.IdpAudience]),

            // Birinci taraf uygulama: musteriye ayrica onay ekrani gosterilmez.
            RequireConsent = false
        };

        var clientScope = new ClientAllowedScope
        {
            Id = EntityId.New(),
            ClientId = client.Id,
            Scope = portalScope.Scope
        };

        await tenantRepository.AddAsync(tenant, ct);
        await userPoolRepository.AddAsync(pool, ct);
        await scopeDefinitionRepository.AddAsync(portalScope, ct);
        await roleRepository.AddAsync(customerRole, ct);
        await roleScopeRepository.AddAsync(customerRoleScope, ct);
        await clientRepository.AddAsync(client, ct);
        await clientAllowedScopeRepository.AddAsync(clientScope, ct);

        await tenantRepository.SaveChangesAsync(ct);
    }

    /// <summary>Ayarlarda tanimli olup client'ta bulunmayan adresleri ekler; hicbir adresi silmez.</summary>
    private async Task SyncClientUrisAsync(CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(
            c => c.Id == AuthConstants.LicensingClientId, enableTracking: true, cancellationToken: ct);

        if (client is null)
            return;

        var redirectAdded = AddMissing(client.RedirectUris, Options.RedirectUris);
        var logoutAdded = AddMissing(client.PostLogoutRedirectUris, PostLogoutUris);

        if (!redirectAdded && !logoutAdded)
            return;

        clientRepository.Update(client);
        await clientRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidateClientAsync(client.ClientKey, ct);
    }

    /// <summary>
    /// Ayardaki cikis adresleri + her ".../signin-oidc" giris adresinin ".../signout-callback-oidc" karsiligi. Portalin
    /// .NET OIDC handler'i cikista bu adrese doner; ayarda unutulursa kullanici IdP'nin "cikis yapildi" ekraninda kaliyordu.
    /// </summary>
    private IEnumerable<string> PostLogoutUris
        => Options.PostLogoutRedirectUris.Concat(Options.RedirectUris
            .Where(u => u.TrimEnd('/').EndsWith("/signin-oidc", StringComparison.OrdinalIgnoreCase))
            .Select(u => u.TrimEnd('/')[..^"/signin-oidc".Length] + "/signout-callback-oidc"));

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
