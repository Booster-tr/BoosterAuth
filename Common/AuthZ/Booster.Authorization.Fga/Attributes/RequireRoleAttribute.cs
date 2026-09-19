using Microsoft.AspNetCore.Authorization;

namespace Booster.Authorization.Fga.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireRoleAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "BoosterRole:";

    public RequireRoleAttribute(params string[] roles)
        => Policy = $"{PolicyPrefix}{string.Join(",", roles)}";
}
