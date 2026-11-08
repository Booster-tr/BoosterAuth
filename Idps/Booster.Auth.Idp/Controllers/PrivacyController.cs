using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class PrivacyController(IdpUiOptions uiOptions) : IdpBaseController
{
    public const string PreferencesPath = "/cookies/preferences";

    private const string SourcePreferences = "preferences";

    [HttpGet("/privacy")]
    public IActionResult Privacy([FromQuery] string? returnUrl)
        => Document("Privacy", 8, returnUrl);

    [HttpGet("/terms")]
    public IActionResult Terms([FromQuery] string? returnUrl)
        => Document("Terms", 9, returnUrl);

    [HttpGet("/explicit-consent")]
    public IActionResult ExplicitConsent([FromQuery] string? returnUrl)
        => Document("ExplicitConsent", 6, returnUrl);

    [HttpGet("/kvkk")]
    public IActionResult Kvkk([FromQuery] string? returnUrl)
        => Document("Kvkk", 7, returnUrl);

    [HttpGet("/cookies")]
    public IActionResult Cookies([FromQuery] string? returnUrl)
        => Document("Cookies", 6, returnUrl);

    [HttpGet(PreferencesPath)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Preferences([FromQuery] string? returnUrl, [FromQuery] bool saved)
    {
        var current = CookieConsent.Read(Request);

        return View(new CookiePreferencesViewModel(
            Functional: current?.Functional ?? false,
            Analytics: current?.Analytics ?? false,
            HasDecision: current is not null,
            Saved: saved && current is not null,
            ReturnUrl: LocalOrNull(returnUrl)));
    }

    [HttpPost("/ui/cookie-consent")]
    [ValidateAntiForgeryToken]
    public IActionResult Save(
        [FromForm] string? choice,
        [FromForm] bool functional,
        [FromForm] bool analytics,
        [FromForm] string? returnUrl,
        [FromForm] string? source)
    {
        var consent = choice switch
        {
            "accept" => CookieConsent.AcceptAll,
            "reject" => CookieConsent.RejectAll,
            _ => new CookieConsent(functional, analytics)
        };

        consent.Write(Response);

        var back = LocalOrNull(returnUrl);

   
        if (source == SourcePreferences || back is null)
        {
            var url = $"{PreferencesPath}?saved=true";
            if (back is not null)
                url += $"&returnUrl={Uri.EscapeDataString(back)}";
            return Redirect(uiOptions.GetAssetUrl(url));
        }

        return Redirect(back);
    }

    private ViewResult Document(string key, int sectionCount, string? returnUrl)
        => View("Document", new LegalDocumentViewModel(key, sectionCount, LocalOrNull(returnUrl)));

    private string? LocalOrNull(string? url)
        => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : null;
}
