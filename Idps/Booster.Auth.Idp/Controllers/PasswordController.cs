using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Password.ForcedChange;
using Booster.Auth.Idp.Features.Password.ForgotPassword;
using Booster.Auth.Idp.Features.Password.ResetPassword;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Translations;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FromForm = Microsoft.AspNetCore.Mvc.FromFormAttribute;
using FromQuery = Microsoft.AspNetCore.Mvc.FromQueryAttribute;
using HttpGet = Microsoft.AspNetCore.Mvc.HttpGetAttribute;
using HttpPost = Microsoft.AspNetCore.Mvc.HttpPostAttribute;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class PasswordController(
    IdpUiOptions uiOptions,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    ISecureCacheStore<ForcedPasswordChangeData> forcedChangeStore,
    ISessionCookieManager cookieManager,
    ITranslationManager translationManager) : IdpBaseController
{
    [HttpGet("/ui/forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromQuery(Name = "interaction_key")] string interactionKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        SetIdentifierField(await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct));

        return View(new ForgotPasswordCommand { InteractionKey = interactionKey });
    }

    [HttpPost("/ui/forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPasswordSubmit(
        [FromForm] ForgotPasswordCommand command, CancellationToken ct)
    {
        try
        {
            await new ForgotPasswordValidator().ValidateAndThrowAsync(command, ct);
            await command.ExecuteAsync(ct);
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            SetIdentifierField(await ResolveClientAsync(interactionStore, clientDataStore, command.InteractionKey, ct));
            return View("ForgotPassword", command);
        }

        return Redirect(uiOptions.GetResetPasswordUrl(command.InteractionKey));
    }

    [HttpGet("/ui/reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromQuery(Name = "interaction_key")] string interactionKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        // Yeni parola kurallari havuz politikasindan gosterilir; gosterilmezse
        // kullanici sunucunun kabul etmeyecegi bir parola yazip sebebini ancak
        // gonderdikten sonra ogrenirdi.
        SetPasswordPolicy(await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct));

        return View(new ResetPasswordCommand { InteractionKey = interactionKey });
    }

    [HttpPost("/ui/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordSubmit(
        [FromForm] ResetPasswordCommand command, CancellationToken ct)
    {
        try
        {
            await new ResetPasswordValidator().ValidateAndThrowAsync(command, ct);
            var result = await command.ExecuteAsync(ct);

            return Redirect(uiOptions.GetLoginUrl(result.InteractionKey!));
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            SetPasswordPolicy(await ResolveClientAsync(interactionStore, clientDataStore, command.InteractionKey, ct));
            return View("ResetPassword", command);
        }
    }

    // --- Giriste zorunlu parola degisimi ---------------------------------------
    // CredentialLoginHandler parolayi dogrulayip MustChangePassword gorurse buraya
    // yonlendirir. Bekleyen kayit yoksa (sure doldu / dogrudan acildi) giris ekranina donulur.

    [HttpGet("/ui/change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromQuery(Name = "interaction_key")] string interactionKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        if (await forcedChangeStore.GetAsync(interactionKey, ct) is null)
            return Redirect(uiOptions.GetLoginUrl(interactionKey));

        SetPasswordPolicy(await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct));

        return View(new ForcedPasswordChangeCommand { InteractionKey = interactionKey });
    }

    [HttpPost("/ui/change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePasswordSubmit(
        [FromForm] ForcedPasswordChangeCommand command, CancellationToken ct)
    {
        command.UserAgent = Request.Headers.UserAgent;
        command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            await new ForcedPasswordChangeValidator().ValidateAndThrowAsync(command, ct);
            var outcome = await command.ExecuteAsync(ct);

            if (outcome.RequiresChallenge)
                return Redirect(uiOptions.GetLoginChallengeUrl(command.InteractionKey));

            cookieManager.Set(Response, outcome.SessionToken!, outcome.ExpiresAt!.Value);
            return Redirect(uiOptions.GetAuthorizeUrl(command.InteractionKey));
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            SetPasswordPolicy(await ResolveClientAsync(interactionStore, clientDataStore, command.InteractionKey, ct));
            return View("ChangePassword", command);
        }
    }

    private async Task AddErrorsAsync(ValidationException ex, CancellationToken ct)
    {
        var lang = ResolveLang();
        foreach (var error in ex.Errors)
        {
            var message = await translationManager.GetAsync(error.ErrorMessage, lang, ct);
            ModelState.AddModelError(error.PropertyName, message);
        }
    }
}
