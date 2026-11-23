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

    // --- WalkToWin mobil uygulaması ------------------------
    // Yönetim kurulumuyla aynı desen: sabit kimlikler, idempotent bootstrapper'lar.

    public static readonly Guid WalkToWinTenantId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    public static readonly Guid WalkToWinPoolId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    /// <summary>Mobil uygulamanın public OAuth client kaydı (client secret yok, PKCE zorunlu).</summary>
    public static readonly Guid WalkToWinClientId = Guid.Parse("00000000-0000-0000-0000-000000000103");

    /// <summary>Kendi kendine kayıt olan her oyuncuya varsayılan atanan rol.</summary>
    public static readonly Guid WalkToWinPlayerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000104");

    public const string WalkToWinTenantName = "WalkToWin";
    public const string WalkToWinPoolName = "WalkToWin Players";
    public const string WalkToWinClientKey = "walk-to-win";
    public const string WalkToWinClientName = "WalkToWin Mobile";

    public const string WalkToWinPlayerRoleName = "player";
    public const string WalkToWinPlayerRoleDisplayName = "Oyuncu";

    /// <summary>Mobil uygulamada av başlatma / oynama izni.</summary>
    public const string WalkToWinPlayScope = "walktowin:play";

    // --- Lisans portali (licensing.appnuraymind.com) -------------------
    // YALNIZCA Booster'in kendi kurulumunda acilir (BootstrapOptions.Licensing.Enabled).
    // Musteri kurulumlarinda bu tenant/havuz/client OLUSMAZ.

    public static readonly Guid LicensingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    /// <summary>Lisans portalina kayit olan musteri kullanicilarinin havuzu.</summary>
    public static readonly Guid LicensingPoolId = Guid.Parse("00000000-0000-0000-0000-000000000202");

    /// <summary>Portalin OAuth client kaydi. Panel gibi PKCE'li authorization code; client secret yok.</summary>
    public static readonly Guid LicensingClientId = Guid.Parse("00000000-0000-0000-0000-000000000203");

    public static readonly Guid LicensingCustomerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000204");

    public const string LicensingTenantName = "Booster Licensing";
    public const string LicensingPoolName = "Booster Musterileri";
    public const string LicensingClientKey = "licensing-portal";
    public const string LicensingClientName = "Booster Lisans Portali";

    public const string LicensingCustomerRoleName = "customer";
    public const string LicensingCustomerRoleDisplayName = "Musteri";

    /// <summary>Lisans portalini kullanma izni.</summary>
    public const string LicensingPortalScope = "licensing:portal";

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
