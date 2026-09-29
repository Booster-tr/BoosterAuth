using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Booster.Authorization.Fga.Handlers;

internal static class TargetSelectorResolver
{
    private static readonly Dictionary<string, Func<HttpContext, string, string?>> Sources =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["route"] = (httpContext, key) => httpContext.GetRouteValue(key)?.ToString(),
            ["query"] = (httpContext, key) => httpContext.Request.Query[key].FirstOrDefault(),
            ["header"] = (httpContext, key) => httpContext.Request.Headers[key].FirstOrDefault(),
            ["claim"] = (httpContext, key) => httpContext.User.FindFirst(key)?.Value,
        };

    public static string? Resolve(string targetSelector, HttpContext httpContext)
    {
        var parts = targetSelector.Split(':', 2);

        if (parts.Length != 2)
            return null;

        return Sources.TryGetValue(parts[0], out var resolver)
            ? resolver(httpContext, parts[1])
            : null;
    }
}
