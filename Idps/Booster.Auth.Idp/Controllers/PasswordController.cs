using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Password.ForgotPassword;
using Booster.Auth.Idp.Features.Password.ResetPassword;
using Booster.Auth.Idp.Options;
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
    ITranslationManager translationManager) : IdpBaseController
{
    [HttpGet("/ui/forgot-password")]
    public IActionResult ForgotPassword([FromQuery(Name = "interaction_key")] string interactionKey)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

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
            return View("ForgotPassword", command);
        }

        return Redirect(uiOptions.GetResetPasswordUrl(command.InteractionKey));
    }

    [HttpGet("/ui/reset-password")]
    public IActionResult ResetPassword([FromQuery(Name = "interaction_key")] string interactionKey)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

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
            return View("ResetPassword", command);
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
