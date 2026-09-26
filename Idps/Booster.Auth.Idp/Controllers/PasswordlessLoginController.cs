using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Login.PasswordlessLogin;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Providers.Abstractions;
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
public class PasswordlessLoginController(
    IdpUiOptions uiOptions,
    ISessionCookieManager cookieManager,
    IProviderFactory providerFactory,
    ITranslationManager translationManager) : IdpBaseController
{
    
    [HttpGet("/ui/passwordless")]
    public IActionResult Start(
        [FromQuery(Name = "interaction_key")] string interactionKey,
        [FromQuery(Name = "provider")] string? provider)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var resolved = providerFactory.GetPasswordlessProvider(provider ?? string.Empty);
        if (resolved is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.ProviderNotAvailable));

        ViewData["IdentifierField"] = IdentifierField.From([resolved.IdentifierType]);
        ViewData["ProviderLabelKey"] = ProviderLabelKey(resolved.ProviderType);

        return View(new StartPasswordlessLoginCommand
        {
            InteractionKey = interactionKey,
            ProviderType = resolved.ProviderType
        });
    }

    [HttpPost("/ui/passwordless")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartSubmit(
        [FromForm] StartPasswordlessLoginCommand command, CancellationToken ct)
    {
        try
        {
            await new StartPasswordlessLoginValidator().ValidateAndThrowAsync(command, ct);
            await command.ExecuteAsync(ct);
        }
        catch (ValidationException ex)
        {
            PrepareStartView(command);
            await AddErrorsAsync(ex, ct);
            return View("Start", command);
        }

        return Redirect(uiOptions.GetPasswordlessVerifyUrl(command.InteractionKey, command.ProviderType));
    }

    // -------------------------------------------------------------- kod alma

    [HttpGet("/ui/passwordless/verify")]
    public IActionResult Verify(
        [FromQuery(Name = "interaction_key")] string interactionKey,
        [FromQuery(Name = "provider")] string? provider)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var resolved = providerFactory.GetPasswordlessProvider(provider ?? string.Empty);
        if (resolved is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.ProviderNotAvailable));

        ViewData["ProviderLabelKey"] = ProviderLabelKey(resolved.ProviderType);

        return View(new VerifyPasswordlessLoginCommand
        {
            InteractionKey = interactionKey,
            ProviderType = resolved.ProviderType
        });
    }

    [HttpPost("/ui/passwordless/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifySubmit(
        [FromForm] VerifyPasswordlessLoginCommand command, CancellationToken ct)
    {
        command.UserAgent = Request.Headers.UserAgent;
        command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            await new VerifyPasswordlessLoginValidator().ValidateAndThrowAsync(command, ct);
            var result = await command.ExecuteAsync(ct);

            cookieManager.Set(Response, result.SessionToken, result.ExpiresAt);

            return Redirect(uiOptions.GetAuthorizeUrl(command.InteractionKey));
        }
        catch (ValidationException ex)
        {
            ViewData["ProviderLabelKey"] = ProviderLabelKey(command.ProviderType);
            await AddErrorsAsync(ex, ct);
            return View("Verify", command);
        }
    }

    // ---------------------------------------------------------------- yardım

    private void PrepareStartView(StartPasswordlessLoginCommand command)
    {
        var resolved = providerFactory.GetPasswordlessProvider(command.ProviderType);

        ViewData["IdentifierField"] = resolved is null
            ? IdentifierField.From([])
            : IdentifierField.From([resolved.IdentifierType]);

        ViewData["ProviderLabelKey"] = ProviderLabelKey(command.ProviderType);
    }

    private static string ProviderLabelKey(string providerType) => providerType switch
    {
        Providers.PhoneCode.PhoneCodeProvider.Type => "Passwordless.Method.Phone",
        _ => "Passwordless.Method.Email"
    };

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
