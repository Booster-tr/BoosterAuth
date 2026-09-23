using Microsoft.AspNetCore.Localization;

namespace Booster.Auth.Idp.Localization;

public sealed class AcceptLanguageCookieProvider : RequestCultureProvider
{
    public const string CookieName = "accept-language";

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var value = httpContext.Request.Cookies[CookieName];

        if (string.IsNullOrWhiteSpace(value))
            return Task.FromResult<ProviderCultureResult?>(null);

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(value.Trim()));
    }
}
