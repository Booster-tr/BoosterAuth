using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Licensing.Portal.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    /// <summary>
    /// Kok adres: tanitim sitesine tarayici diliyle yonlendirir (/tr ya da /en; dil secimi kalici olmadigi icin 302).
    /// Urun panelindeki "Lisans al" butonu kurulum GUID'ini tasir (?installationId=): o zaman giris yapilir ve kurulum
    /// formu GUID dolu acilir.
    /// </summary>
    [HttpGet("/")]
    public IActionResult Index([FromQuery] Guid? installationId)
    {
        if (installationId is { } g && g != Guid.Empty)
        {
            var next = $"/hesap/kurulumlar/yeni?installationId={g}";
            return User.Identity?.IsAuthenticated == true
                ? Redirect(next)
                : Redirect($"/giris?returnUrl={Uri.EscapeDataString(next)}");
        }
        Response.Headers.Vary = "Accept-Language";
        return Redirect("/" + Site.SiteCatalog.PreferredLanguage(Request.Headers.AcceptLanguage));
    }

    /// <summary>Booster Auth giris ekranina yonlendirir. Kayit ("Hesap olustur") da o ekrandadir.</summary>
    [HttpGet("/giris")]
    public IActionResult Login(string? returnUrl = null) =>
        Challenge(new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/" },
            OpenIdConnectDefaults.AuthenticationScheme);

    [HttpPost("/cikis")]
    public IActionResult Logout() =>
        SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);

    [HttpGet("/yetkisiz")]
    public IActionResult Forbidden() => View();

    [HttpGet("/hata")]
    public IActionResult Error() => View();

    /// <summary>
    /// Bulunamayan adresler (UseStatusCodePagesWithReExecute). Dil istenen adresten secilir (/en/... Ingilizce); once
    /// bos govdeli 404 donuyordu.
    /// </summary>
    [HttpGet("/bulunamadi")]
    public IActionResult NotFoundPage()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>();
        Response.StatusCode = feature?.OriginalStatusCode ?? StatusCodes.Status404NotFound;

        // Yalnizca 404 "bulunamadi"dir; govdesiz baska hatalar (400 vb.) genel hata ekranini gosterir.
        if (Response.StatusCode != StatusCodes.Status404NotFound)
            return View("Error");

        ViewData["En"] = (feature?.OriginalPath ?? string.Empty).StartsWith("/en", StringComparison.OrdinalIgnoreCase);
        return View("NotFound");
    }
}
