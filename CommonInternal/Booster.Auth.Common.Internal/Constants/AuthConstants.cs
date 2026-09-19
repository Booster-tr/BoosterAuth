namespace Booster.Auth.Common.Internal.Constants;

public static class AuthConstants
{
    public static readonly Guid ManagementPoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static readonly Guid ManagementTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public const string ManagementStoreName = "management";

    public const string UserPrefix = "user";
    public const string TenantType = "tenant";
    public const string PoolType = "pool";
    public const string ClientType = "client";

    public const string OwnerRelation = "owner";
    public const string ManagerRelation = "manager";
    public const string ViewerRelation = "viewer";
    public const string ParentRelation = "parent";

    public const string EndUserRelation = "end_user";

    public static string User(Guid id) => $"{UserPrefix}:{id}";

    public static string Tenant(Guid id) => $"{TenantType}:{id}";

    public static string Pool(Guid id) => $"{PoolType}:{id}";

    public static string Client(Guid id) => $"{ClientType}:{id}";
}
