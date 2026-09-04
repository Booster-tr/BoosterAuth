using Booster.Auth.Manager.RequestContexts;
using Microsoft.AspNetCore.Http;

namespace Booster.Auth.Manager.Middlewares;

public class RequestContextMiddleware(RequestDelegate next)
{

    public async Task InvokeAsync(
        HttpContext context,
        IRequestContextAccessor requestContextAccessor)
    {

        var acceptLanguage = ResolvePreferredLanguage(context);

        requestContextAccessor.RequestContext = RequestContext.ForSystem();
        if (context.User.Identity is { IsAuthenticated: true })
        {
            var user = context.User;

            Guid.TryParse(user.FindFirst("sub")?.Value, out var principalId);

            Guid? sessionId = Guid.TryParse(user.FindFirst("sid")?.Value, out var sid)
                ? sid : null;

            var clientId = user.FindFirst("azp")?.Value ?? string.Empty;

            var principalType = user.FindFirst("principal_type")?.Value ?? "user";

            var accessToken = ExtractBearerToken(context);

            var isolation = Guid.TryParse(user.FindFirst("isolation")?.Value, out var iso)
             ? iso
             : Guid.Empty;


            requestContextAccessor.RequestContext = RequestContext.ForPrincipal(
                principalId, principalType, sessionId, clientId, accessToken, acceptLanguage,
                isolation);
        }

        await next(context);
    }

    private static string ResolvePreferredLanguage(HttpContext context)
    {
        var query = Normalize(context.Request.Headers["accept-language"].FirstOrDefault());
        if (query is not null)
            return query;

        return Normalize(context.Request.Cookies["accept-language"]) ?? "tr-tr";
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim().ToLowerInvariant();
        return value.Length > 5 ? value[..5] : value;
    }

    private static string ExtractBearerToken(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].ToString();
        return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : string.Empty;
    }
}