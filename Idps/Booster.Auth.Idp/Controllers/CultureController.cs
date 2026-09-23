using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Localization;
using Booster.Auth.Idp.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class CultureController(
    IOptions<RequestLocalizationOptions> localizationOptions,
    IdpUiOptions uiOptions) : IdpBaseController
{
    [HttpPost("/ui/culture")]
    [ValidateAntiForgeryToken]
    public IActionResult Set([FromForm] string culture, [FromForm] string? returnUrl)
    {
        var supported = localizationOptions.Value.SupportedUICultures ?? [];
        var match = supported.FirstOrDefault(c =>
            string.Equals(c.Name, culture, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            Response.Cookies.Append(
                AcceptLanguageCookieProvider.CookieName,
                match.Name,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Path = "/",
                    IsEssential = true
                });
        }

        return Redirect(Url.IsLocalUrl(returnUrl)
            ? returnUrl!
            : uiOptions.GetErrorUrl("invalid_request"));
    }
}
