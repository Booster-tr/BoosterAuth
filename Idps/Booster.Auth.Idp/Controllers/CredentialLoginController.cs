using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Login.CredentialLogin;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Options;
using Booster.ExceptionManager.Abstraction.Translations;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class CredentialLoginController(IdpUiOptions uiOptions,
    ISessionCookieManager cookieManager,
    ITranslationManager translationManager) : IdpBaseController
{
    [Microsoft.AspNetCore.Mvc.HttpGet("/ui/login")]
    public IActionResult Index([Microsoft.AspNetCore.Mvc.FromQuery(Name = "interaction_key")] string interactionKey)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        return View(new CredentialLoginCommand { InteractionKey = interactionKey });
    }

    [Microsoft.AspNetCore.Mvc.HttpPost("/ui/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([Microsoft.AspNetCore.Mvc.FromForm] CredentialLoginCommand command, CancellationToken ct)
    {
        command.UserAgent = Request.Headers.UserAgent;
        command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            await new CredentialLoginValidator().ValidateAndThrowAsync(command, ct);
            var result = await command.ExecuteAsync(ct);

            cookieManager.Set(Response, result.SessionToken, result.ExpiresAt);
            return Redirect(uiOptions.GetAuthorizeUrl(command.InteractionKey));
        }
        catch (ValidationException ex)
        {
            var lang = ResolveLang();
            foreach (var err in ex.Errors)
            {
                var msg = await translationManager.GetAsync(err.ErrorMessage, lang, ct);
                ModelState.AddModelError(err.PropertyName, msg);
            }
            return View("Index", command);
        }
    }
}
