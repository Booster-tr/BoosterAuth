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

    // --- Resource tipleri --------------------------------------------------

    public const string UserPrefix = "user";
    public const string TenantType = "tenant";
    public const string PoolType = "pool";
    public const string ClientType = "client";

    // --- Relation isimleri -------------------------------------------------

    public const string OwnerRelation = "owner";
    public const string ManagerRelation = "manager";
    public const string ViewerRelation = "viewer";
    public const string ParentRelation = "parent";

    public const string EndUserRelation = "end_user";

    // --- Tuple biçimlendirme ----------------------------------------------

    public static string User(Guid id) => $"{UserPrefix}:{id}";

    public static string Tenant(Guid id) => $"{TenantType}:{id}";

    public static string Pool(Guid id) => $"{PoolType}:{id}";

    public static string Client(Guid id) => $"{ClientType}:{id}";
}
