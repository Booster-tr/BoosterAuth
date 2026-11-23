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
    /// Urun tanitim sayfasi. Urun panelindeki "Lisans al" butonu kurulum GUID'ini tasir (?installationId=): o zaman
    /// sayfa gosterilmez; giris yapilir ve kurulum formu GUID dolu acilir.
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
        return View();
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
