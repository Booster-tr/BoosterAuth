using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.LegalManagers;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class PrivacyController(IdpUiOptions uiOptions, LegalContentService legalContent) : IdpBaseController
{
    public const string PreferencesPath = "/cookies/preferences";

    private const string SourcePreferences = "preferences";

    [HttpGet("/privacy")]
    public Task<IActionResult> Privacy([FromQuery] string? returnUrl, CancellationToken ct)
        => Document(LegalDocumentKeys.Privacy, returnUrl, ct);

    [HttpGet("/terms")]
    public Task<IActionResult> Terms([FromQuery] string? returnUrl, CancellationToken ct)
        => Document(LegalDocumentKeys.Terms, returnUrl, ct);

    [HttpGet("/explicit-consent")]
    public Task<IActionResult> ExplicitConsent([FromQuery] string? returnUrl, CancellationToken ct)
        => Document(LegalDocumentKeys.ExplicitConsent, returnUrl, ct);

    [HttpGet("/kvkk")]
    public Task<IActionResult> Kvkk([FromQuery] string? returnUrl, CancellationToken ct)
        => Document(LegalDocumentKeys.Kvkk, returnUrl, ct);

    [HttpGet("/cookies")]
    public Task<IActionResult> Cookies([FromQuery] string? returnUrl, CancellationToken ct)
        => Document(LegalDocumentKeys.Cookies, returnUrl, ct);

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

    /// <summary>Metin veritabanindan (uygulama sahibinin metni), secili dilde; o dilde yoksa diger dilde.</summary>
    private async Task<IActionResult> Document(string key, string? returnUrl, CancellationToken ct)
    {
        var page = await legalContent.GetPageAsync(key, System.Globalization.CultureInfo.CurrentUICulture.Name, ct);
        return page is null
            ? NotFound()
            : View("Document", new LegalDocumentViewModel(page, LocalOrNull(returnUrl)));
    }

    private string? LocalOrNull(string? url)
        => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : null;
}
