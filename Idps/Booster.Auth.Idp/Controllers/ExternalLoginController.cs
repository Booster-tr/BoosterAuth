using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features.Login.FederationLogin;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Caching.Abstraction;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using FromQuery = Microsoft.AspNetCore.Mvc.FromQueryAttribute;
using HttpGet = Microsoft.AspNetCore.Mvc.HttpGetAttribute;
using HttpPost = Microsoft.AspNetCore.Mvc.HttpPostAttribute;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class ExternalLoginController(
    IdpUiOptions uiOptions,
    ISessionCookieManager cookieManager,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore) : IdpBaseController
{
    [HttpGet("/ui/external/challenge")]
    public async Task<IActionResult> Challenge(
        [FromQuery(Name = "interaction_key")] string interactionKey,
        [FromQuery(Name = "provider")] string? provider,
        [FromQuery(Name = "login_hint")] string? loginHint,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey) || string.IsNullOrEmpty(provider))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var command = new StartFederationLoginCommand
        {
            InteractionKey = interactionKey,
            ProviderKey = provider,
            LoginHint = loginHint
        };

        try
        {
            await new StartFederationLoginValidator().ValidateAndThrowAsync(command, ct);

            var result = await command.ExecuteAsync(ct);

            return Redirect(result.RedirectUrl);
        }
        catch (ValidationException)
        {
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.FederationProviderNotFound));
        }
    }

    // ---------------------------------------------------------------- geri don

    /// <summary>
    /// Dönüş GET ile de POST ile de gelebilir: OAuth2 sorgu dizesi kullanır,
    /// SAML ve izin isteyen Apple akışı <c>form_post</c> kullanır. İkisini de
    /// karşılamak zorundayız, yoksa o sağlayıcılar hiç çalışmazdı.
    /// </summary>
    [HttpGet("/ui/external/callback")]
    public Task<IActionResult> CallbackGet(CancellationToken ct)
        => HandleCallbackAsync(ReadParameters(), ct);

    [HttpPost("/ui/external/callback")]
    // Antiforgery YOK ve olamaz: isteği dış sağlayıcı gönderiyor, bizim
    // token'ımızı taşıyamaz. Yerini tek kullanımlık state tutuyor.
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> CallbackPost(CancellationToken ct)
        => HandleCallbackAsync(ReadParameters(), ct);

    private async Task<IActionResult> HandleCallbackAsync(
        Dictionary<string, string> parameters, CancellationToken ct)
    {
        if (!parameters.TryGetValue("state", out var state) || string.IsNullOrEmpty(state))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.FederationStateInvalid));

        var command = new CompleteFederationLoginCommand
        {
            State = state,
            Parameters = parameters,
            UserAgent = Request.Headers.UserAgent,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await command.ExecuteAsync(ct);

        // E-posta zaten yerel bir hesapta: oturum AÇILMAZ, kullanıcı durumu
        // anlatan ekrana gönderilir.
        if (result.Outcome == FederationOutcome.LocalAccountExists)
            return Redirect(uiOptions.GetExternalConflictUrl(result.InteractionKey, result.Email!));

        cookieManager.Set(Response, result.SessionToken!, result.ExpiresAt!.Value);

        return Redirect(uiOptions.GetAuthorizeUrl(result.InteractionKey));
    }

    /// <summary>
    /// Sorgu dizesini ve varsa form gövdesini tek sözlükte toplar. Form ÖNCE
    /// okunmaz; sorgu dizesi temel, form onu tamamlar.
    /// </summary>
    private Dictionary<string, string> ReadParameters()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in Request.Query)
            parameters[key] = value.ToString();

        if (Request.HasFormContentType)
        {
            foreach (var (key, value) in Request.Form)
                parameters[key] = value.ToString();
        }

        return parameters;
    }

    // ------------------------------------------------------------ catisma ekrani

    /// <summary>
    /// "Bu e-posta zaten kayıtlı" ekranı.
    /// <para>
    /// Ayrı bir ekran olması gerekiyor çünkü burada kullanıcıya bir KARAR
    /// sunuluyor: parolasıyla girmek ya da parolasını sıfırlamak. Hata ekranına
    /// düşürmek, yapılabilecek bir şey varken çıkmaz sokak göstermek olurdu.
    /// </para>
    /// </summary>
    [HttpGet("/ui/external/conflict")]
    public async Task<IActionResult> Conflict(
        [FromQuery(Name = "interaction_key")] string interactionKey,
        [FromQuery(Name = "email")] string? email,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction));

        var client = await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct);
        if (client is null)
            return Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.ClientNotFound));

        SetIdentifierField(client);

        return View(new ExternalConflictModel
        {
            InteractionKey = interactionKey,
            Email = email,
            LoginUrl = uiOptions.GetLoginUrl(interactionKey),
            ForgotPasswordUrl = uiOptions.GetForgotPasswordUrl(interactionKey)
        });
    }
}
