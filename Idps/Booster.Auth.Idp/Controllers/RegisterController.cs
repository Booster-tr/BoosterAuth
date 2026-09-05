using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Register;
using Booster.Auth.Idp.Options;
using Booster.ExceptionManager.Abstraction.Translations;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

public class RegisterController(
    IdpUiOptions uiOptions,
    ITranslationManager translationManager) : IdpBaseController
{
    [Microsoft.AspNetCore.Mvc.HttpGet("/register")]
    public IActionResult Index([Microsoft.AspNetCore.Mvc.FromQuery(Name = "interaction_key")] string interactionKey)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        return View(new RegisterCommand { InteractionKey = interactionKey });
    }

    [Microsoft.AspNetCore.Mvc.HttpPost("/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([Microsoft.AspNetCore.Mvc.FromForm] RegisterCommand command, CancellationToken ct)
    {
        try
        {
            await new RegisterValidator().ValidateAndThrowAsync(command, ct);
            await command.ExecuteAsync(ct);

            return Redirect(uiOptions.GetLoginUrl(command.InteractionKey));
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