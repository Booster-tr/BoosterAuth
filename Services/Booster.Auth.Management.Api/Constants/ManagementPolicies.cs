using Booster.Auth.Common.Internal.Constants;

namespace Booster.Auth.Management.Api.Constants;

public static class ManagementPolicies
{
    public const string TenantTarget = "query:TenantId";

    public const string PoolTarget = "query:UserPoolId";

    public const string ClientTarget = "query:ClientId";

    public const string IsolationTarget = "claim:actor";

    public const string PanelScope = AuthConstants.ManagementPanelScope;
}
