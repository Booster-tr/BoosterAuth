using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Managers.LoginChallengeManagers;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Translations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

/// <summary>
/// Parola (ya da kayıt) sonrası, oturum açılmadan önceki adımlar:
/// e-posta doğrulama, telefon doğrulama ve iki adımlı doğrulama (MFA).
/// Adımların kendisi <see cref="ILoginChallengeManager"/>'dadır; burası yalnızca ekrandır.
/// </summary>
[AllowAnonymous]
public class LoginChallengeController(
    IdpUiOptions uiOptions,
    ISessionCookieManager cookieManager,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    ILoginChallengeManager challengeManager,
    ITranslationManager translationManager) : IdpBaseController
{
    [HttpGet("/ui/login/verify")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "interaction_key")] string interactionKey,
        [FromQuery(Name = "resent")] string? resent,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var client = await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct);
        if (client is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var challenge = await challengeManager.GetAsync(interactionKey, client, ct);

        // Yarım giriş yoksa (süresi doldu ya da adres elle açıldı) parola
        // yeniden istenir; doğrulama ekranı tek başına hiçbir şey açmaz.
        if (challenge is null)
            return Redirect(uiOptions.GetLoginUrl(interactionKey));

        return View(LoginChallengeViewModel.From(interactionKey, challenge, resent == "true", resent == "wait"));
    }

    [HttpPost("/ui/login/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromForm] LoginChallengeViewModel model, CancellationToken ct)
    {
        var client = await ResolveClientAsync(interactionStore, clientDataStore, model.InteractionKey, ct);
        if (client is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        try
        {
            await new LoginChallengeValidator().ValidateAndThrowAsync(model, ct);

            var outcome = await challengeManager.VerifyAsync(model.InteractionKey, client, model.Code!, ct);

            // Sıradaki adım varsa (ör. e-posta doğrulandı, sırada MFA) ekran yenilenir.
            if (outcome.RequiresChallenge)
                return Redirect(uiOptions.GetLoginChallengeUrl(model.InteractionKey));

            cookieManager.Set(Response, outcome.SessionToken!, outcome.ExpiresAt!.Value);
            return Redirect(uiOptions.GetAuthorizeUrl(model.InteractionKey));
        }
        catch (ValidationException ex) when (IsExpired(ex))
        {
            return Redirect(uiOptions.GetLoginUrl(model.InteractionKey));
        }
        catch (ValidationException ex)
        {
            var challenge = await challengeManager.GetAsync(model.InteractionKey, client, ct);
            if (challenge is null)
                return Redirect(uiOptions.GetLoginUrl(model.InteractionKey));

            await AddErrorsAsync(ex, ct);
            return View("Index", LoginChallengeViewModel.From(model.InteractionKey, challenge, resent: false));
        }
    }

    [HttpPost("/ui/login/verify/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend([FromForm] string interactionKey, CancellationToken ct)
    {
        var sent = true;
        var result = await RunAsync(interactionKey, async (client, token) => sent = await challengeManager.ResendAsync(interactionKey, client, token), resent: true, ct);
        // Bekleme suresi dolmadan istenirse kod gitmez; ekran "gonderildi" yerine "bekleyin" der.
        return !sent && result is RedirectResult r ? Redirect(r.Url.Replace("resent=true", "resent=wait")) : result;
    }

    [HttpPost("/ui/login/verify/method")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectMethod(
        [FromForm] string interactionKey, [FromForm] ChallengeChannel channel, CancellationToken ct)
        => await RunAsync(interactionKey, (client, token) => challengeManager.SelectChannelAsync(interactionKey, client, channel, token), resent: true, ct);

    // ---------------------------------------------------------------- yardım

    private async Task<IActionResult> RunAsync(
        string interactionKey,
        Func<Booster.Auth.Database.Models.ClientData, CancellationToken, Task> action,
        bool resent,
        CancellationToken ct)
    {
        var client = await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct);
        if (client is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        try
        {
            await action(client, ct);
        }
        catch (ValidationException)
        {
            return Redirect(uiOptions.GetLoginUrl(interactionKey));
        }

        var url = uiOptions.GetLoginChallengeUrl(interactionKey);
        return Redirect(resent ? $"{url}&resent=true" : url);
    }

    private static bool IsExpired(ValidationException ex)
        => ex.Errors.Any(e => e.ErrorMessage is IdpMessageKeys.LoginChallengeExpired or IdpMessageKeys.InvalidInteraction);

    private async Task AddErrorsAsync(ValidationException ex, CancellationToken ct)
    {
        var lang = ResolveLang();
        foreach (var err in ex.Errors)
        {
            var msg = await translationManager.GetAsync(err.ErrorMessage, lang, ct);
            ModelState.AddModelError(err.PropertyName, msg);
        }
    }
}
