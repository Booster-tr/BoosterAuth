using Booster.Auth.Common.Internal.Constants;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts._base;

public static class ServiceAccountRules
{
    public static readonly string[] Assignable =
    [
        AuthConstants.ViewerRelation,
        AuthConstants.ManagerRelation,
        AuthConstants.GrantManagerRelation
    ];

    public static string Isolation => AuthConstants.ManagementPoolId.ToString();
}
