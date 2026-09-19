using Microsoft.AspNetCore.Authorization;

namespace Booster.Authorization.Fga.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireScopeAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "BoosterScope:";

    public RequireScopeAttribute(params string[] scopes)
        => Policy = $"{PolicyPrefix}{string.Join(",", scopes)}";
}
