using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Login.CredentialLogin;
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

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class CredentialLoginController(IdpUiOptions uiOptions,
    ISessionCookieManager cookieManager,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    ITranslationManager translationManager) : IdpBaseController
{
    [Microsoft.AspNetCore.Mvc.HttpGet("/ui/login")]
    public async Task<IActionResult> Index(
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "interaction_key")] string interactionKey,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var client = await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct);
        SetIdentifierField(client);
        SetPasswordlessMethods(client);
        SetFederationMethods(client);

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

            if (result.RequiresPasswordChange)
                return Redirect(uiOptions.GetChangePasswordUrl(command.InteractionKey));

            if (result.RequiresChallenge)
                return Redirect(uiOptions.GetLoginChallengeUrl(command.InteractionKey));

            cookieManager.Set(Response, result.SessionToken!, result.ExpiresAt!.Value);
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
            var errorClient = await ResolveClientAsync(interactionStore, clientDataStore, command.InteractionKey, ct);
            SetIdentifierField(errorClient);
            SetPasswordlessMethods(errorClient);
            SetFederationMethods(errorClient);
            return View("Index", command);
        }
    }
}
