namespace Booster.Auth.Common.Internal.Constants;

public static class AuthConstants
{
    // --- Sabit kimlikler ---------------------------------------------------

    public static readonly Guid ManagementPoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static readonly Guid ManagementTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>Yönetim arayüzünün (NextJs) OAuth client kaydı.</summary>
    public static readonly Guid ManagementClientId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    /// <summary>Yönetim havuzunun admin rolü.</summary>
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000005");

    // --- Görünen adlar / anahtarlar ---------------------------------------

    public const string ManagementStoreName = "management";
    public const string ManagementTenantName = "Booster";
    public const string ManagementPoolName = "Booster Auth";
    public const string ManagementClientKey = "management";
    public const string ManagementClientName = "Booster Management";

    // --- Admin kimlik bilgileri -------------------------------------------
    // Parola BURADA YOKTUR ve olmamalıdır: her servis kendi
    // appsettings/user-secrets dosyasından BootstrapOptions ile okur.

    public const string AdminEmail = "admin@booster.local";
    public const string AdminUserName = "admin";
    public const string AdminDisplayName = "System Admin";

    public const string AdminRoleName = "admin";
    public const string AdminRoleDisplayName = "Sistem Yöneticisi";

    /// <summary>Yönetim arayüzüne erişim izni. Havuzda tanımlı olmak zorundadır.</summary>
    public const string ManagementPanelScope = "booster:panel";

    // --- Lisans portali (licensing.appnuraymind.com) -------------------
    // YALNIZCA Booster'in kendi kurulumunda acilir (BootstrapOptions.Licensing.Enabled).
    // Musteri kurulumlarinda bu tenant/havuz/client OLUSMAZ.

    public static readonly Guid LicensingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    /// <summary>Lisans portalina kayit olan musteri kullanicilarinin havuzu.</summary>
    public static readonly Guid LicensingPoolId = Guid.Parse("00000000-0000-0000-0000-000000000202");

    /// <summary>Portalin OAuth client kaydi. Panel gibi PKCE'li authorization code; client secret yok.</summary>
    public static readonly Guid LicensingClientId = Guid.Parse("00000000-0000-0000-0000-000000000203");

<<<<<<< Updated upstream
    public static readonly Guid LicensingCustomerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000204");
=======
    /// <summary>Kendi kendine kayıt olan her oyuncuya varsayılan atanan rol (hunter).</summary>
    public static readonly Guid WalkToWinHunterRoleId = Guid.Parse("00000000-0000-0000-0000-000000000104");

    /// <summary>Av oluşturan / yöneten kullanıcı rolü; panelden atanır.</summary>
    public static readonly Guid WalkToWinHuntCreatorRoleId = Guid.Parse("00000000-0000-0000-0000-000000000105");

    /// <summary>WalkToWin yöneticisi; tüm avları yönetir. Panelden atanır.</summary>
    public static readonly Guid WalkToWinAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000106");
>>>>>>> Stashed changes

    public const string LicensingTenantName = "Booster Licensing";
    public const string LicensingPoolName = "Booster Musterileri";
    public const string LicensingClientKey = "licensing-portal";
    public const string LicensingClientName = "Booster Lisans Portali";

<<<<<<< Updated upstream
    public const string LicensingCustomerRoleName = "customer";
    public const string LicensingCustomerRoleDisplayName = "Musteri";

    /// <summary>Lisans portalini kullanma izni.</summary>
    public const string LicensingPortalScope = "licensing:portal";
=======
    public const string WalkToWinHunterRoleName = "hunter";
    public const string WalkToWinHunterRoleDisplayName = "Avcı";

    /// <summary>İlk kurulumdaki rol anahtarı; mevcut kurulumlarda bootstrapper "hunter"a çevirir.</summary>
    public const string WalkToWinLegacyPlayerRoleName = "player";

    public const string WalkToWinHuntCreatorRoleName = "hunt_creator";
    public const string WalkToWinHuntCreatorRoleDisplayName = "Av Oluşturucu";

    public const string WalkToWinAdminRoleName = "admin";
    public const string WalkToWinAdminRoleDisplayName = "WalkToWin Yöneticisi";

    /// <summary>Mobil uygulamada av başlatma / oynama izni. Client'ın izinli tek scope'u.</summary>
    public const string WalkToWinPlayScope = "walktowin:play";
>>>>>>> Stashed changes

    /// <summary>
    /// Av oluşturma / yönetme. Client'ın AllowedScopes'una BİLEREK eklenmez: IdP /authorize'da istenen scope'u
    /// yalnızca client'a göre denetler; eklenseydi her oyuncu bu scope'u isteyip alırdı. Token'a yalnızca
    /// kullanıcıya rol atanırken yazılan PrincipalScope'tan girer.
    /// </summary>
    public const string WalkToWinHuntsManageScope = "walktowin:hunts.manage";

    /// <summary>WalkToWin yöneticisi; HuntsManage ile aynı nedenle client'ta izinli değildir.</summary>
    public const string WalkToWinAdminScope = "walktowin:admin";

    // WalkToWin havuzunun kendi AuthZ şeması (store: pool-{WalkToWinPoolId}).
    // workspace tek nesnedir: workspace:{WalkToWinPoolId} (token'daki actor ile aynı değer).
    public const string WalkToWinWorkspaceType = "workspace";
    public const string WalkToWinHuntType = "hunt";
    public const string WalkToWinAdminRelation = "admin";
    public const string WalkToWinCreatorRelation = "creator";

    // --- Resource tipleri --------------------------------------------------

    public const string UserPrefix = "user";
    public const string TenantType = "tenant";
    public const string PoolType = "pool";
    public const string ClientType = "client";

    // --- Relation isimleri -------------------------------------------------

    public const string OwnerRelation = "owner";
    public const string ManagerRelation = "manager";
    public const string ViewerRelation = "viewer";

    /// <summary>Yetki verme / geri alma. Owner'a otomatik geçer; grant manager aynı zamanda viewer'dır.</summary>
    public const string GrantManagerRelation = "grant_manager";
    public const string ParentRelation = "parent";

    public const string EndUserRelation = "end_user";

    // --- Tuple biçimlendirme ----------------------------------------------

    public static string User(Guid id) => $"{UserPrefix}:{id}";

    public static string Tenant(Guid id) => $"{TenantType}:{id}";

    public static string Pool(Guid id) => $"{PoolType}:{id}";

    public static string Client(Guid id) => $"{ClientType}:{id}";
}
