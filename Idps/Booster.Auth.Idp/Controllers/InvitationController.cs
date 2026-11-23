using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Invitations;
using Booster.Auth.Idp.Managers.UserManagers;
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

/// <summary>
/// Davet baglantisi: panelden eklenen kullanici parolasini burada belirler. OAuth etkilesimi (interaction_key)
/// YOKTUR; kullanici sonra kendi uygulamasindan normal giris yapar.
/// </summary>
[AllowAnonymous]
public class InvitationController(
    IdpUiOptions uiOptions,
    InvitationManager invitations,
    PoolDataReader poolDataReader,
    ITranslationManager translationManager) : IdpBaseController
{
    [HttpGet("/ui/invite")]
    public async Task<IActionResult> Index([FromQuery(Name = "token")] string? token, CancellationToken ct)
    {
        var invitation = await invitations.ResolveAsync(token, ct);
        if (invitation is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvitationInvalid));

        await PrepareAsync(invitation, ct);
        return View(new AcceptInvitationCommand { Token = token! });
    }

    [HttpPost("/ui/invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromForm] AcceptInvitationCommand command, CancellationToken ct)
    {
        try
        {
            await command.ExecuteAsync(ct);
            return View("Done");
        }
        catch (ValidationException ex)
        {
            var lang = ResolveLang();
            foreach (var error in ex.Errors)
                ModelState.AddModelError(error.PropertyName, await translationManager.GetAsync(error.ErrorMessage, lang, ct));

            var invitation = await invitations.ResolveAsync(command.Token, ct);
            if (invitation is null)
                return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvitationInvalid));

            await PrepareAsync(invitation, ct);
            return View("Index", command);
        }
    }

    /// <summary>Parola kurallari havuz politikasindan gelir; formda gosterilir.</summary>
    private async Task PrepareAsync(InvitationData invitation, CancellationToken ct)
    {
        ViewData["InvitationEmail"] = invitation.Email;
        SetPasswordPolicy(await poolDataReader.GetAsync(invitation.IsolationId, ct));
    }
}
