using Microsoft.AspNetCore.Authorization;

namespace Booster.Authorization.Fga.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireFgaAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "BoosterFga:";

    public RequireFgaAttribute(string resource, string targetSelector, string permission)
        => Policy = $"{PolicyPrefix}{resource}|{targetSelector}|{permission}";
}
