using Booster.Licensing.Portal.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Licensing.Portal.Controllers;

[AllowAnonymous]
public sealed class HomeController(LicensingPortalOptions options) : Controller
{
    [HttpGet("/")]
    public IActionResult Index([FromQuery] Guid? installationId)
    {
        // Urun panelindeki "Lisans al" butonu kurulum GUID'ini tasir; giristen sonra kurulum formu dolu acilir.
        var next = installationId is { } g && g != Guid.Empty ? $"/hesap/kurulumlar/yeni?installationId={g}" : "/hesap";
        if (User.Identity?.IsAuthenticated != true)
        {
            ViewBag.Next = next;
            return View();
        }
        if (string.Equals(User.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase) && options.IsAdmin(User.FindFirst("email")?.Value))
            return Redirect("/admin");
        return Redirect(next);
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
}
