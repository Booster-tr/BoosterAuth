using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Register;
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
public class RegisterController(
    IdpUiOptions uiOptions,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    ITranslationManager translationManager) : IdpBaseController
{
    [Microsoft.AspNetCore.Mvc.HttpGet("/register")]
    public async Task<IActionResult> Index(
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "interaction_key")] string interactionKey,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        // İstemci ÇÖZÜLÜR ve çözülemezse form hiç gösterilmez: hangi alanların
        // zorunlu olduğu ve parola kuralları havuz politikasından geliyor,
        // politikasız bir form kullanıcıyı dolduramayacağı bir ekrana oturtur.
        var client = await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct);
        if (client is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.ClientNotFound));

        SetRegistrationRequirements(client);
        SetPasswordPolicy(client);

        return View(new RegisterCommand { InteractionKey = interactionKey });
    }

    [Microsoft.AspNetCore.Mvc.HttpPost("/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        [Microsoft.AspNetCore.Mvc.FromForm] RegisterCommand command, CancellationToken ct)
    {
        try
        {
            command.UserAgent = Request.Headers.UserAgent;
            command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await new RegisterValidator().ValidateAndThrowAsync(command, ct);
            var result = await command.ExecuteAsync(ct);

            return Redirect(result.RequiresChallenge
                ? uiOptions.GetLoginChallengeUrl(command.InteractionKey)
                : uiOptions.GetLoginUrl(command.InteractionKey));
        }
        catch (ValidationException ex)
        {
            // Hata ile dönen formda politika YENİDEN set edilir; edilmezse
            // kullanıcı ikinci denemede kurallardan ve zorunlu alan
            // işaretlerinden yoksun bir form görürdü.
            var client = await ResolveClientAsync(
                interactionStore, clientDataStore, command.InteractionKey, ct);

            SetRegistrationRequirements(client);
            SetPasswordPolicy(client);

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
