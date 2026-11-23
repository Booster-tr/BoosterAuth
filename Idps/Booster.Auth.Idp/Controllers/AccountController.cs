using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Managers.AccountManagers;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Translations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

/// <summary>
/// "Hesabim": son kullanicinin kendi hesabini yonettigi sayfalar. Uygulama tek bir baglanti koyar:
/// <c>{issuer}/ui/account?client_id={uygulama}&amp;return_url={uygulamadaki donus adresi}</c>.
/// Kimlik IdP oturum cerezinden gelir; oturum yoksa normal giris ekrani acilir ve giristen sonra buraya donulur.
/// </summary>
[AllowAnonymous]
public class AccountController(
    AccountService accounts,
    ISessionCookieManager cookieManager,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IdpUiOptions uiOptions,
    ITranslationManager translationManager) : IdpBaseController
{
    private static readonly TimeSpan LoginInteractionTtl = TimeSpan.FromMinutes(30);

    // --- Ozet ------------------------------------------------------------------------------------------------

    [HttpGet("/ui/account")]
    public async Task<IActionResult> Index([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, [FromQuery(Name = "done")] string? done, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, string.Empty, ct);
        if (ctx is null) return stop!;

        var model = Model(ctx, clientKey!, returnUrl);
        model.Notice = done;
        model.ActiveSessionCount = (await accounts.ListSessionsAsync(ctx, ct)).Count;
        return View(model);
    }

    // --- Ad soyad --------------------------------------------------------------------------------------------

    [HttpGet("/ui/account/profile")]
    public async Task<IActionResult> Profile([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, "/profile", ct);
        if (ctx is null) return stop!;
        if (ctx.DirectoryManaged) return RedirectToIndex(clientKey!, returnUrl);

        var model = Model(ctx, clientKey!, returnUrl);
        model.DisplayName = ctx.Principal.DisplayName;
        return View(model);
    }

    [HttpPost("/ui/account/profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileSubmit([FromForm] AccountForm form, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(form.ClientId, form.ReturnUrl, "/profile", ct);
        if (ctx is null) return stop!;

        try
        {
            await accounts.UpdateDisplayNameAsync(ctx, form.DisplayName, ct);
            return RedirectToIndex(form.ClientId!, form.ReturnUrl, "profile");
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            var model = Model(ctx, form.ClientId!, form.ReturnUrl);
            model.DisplayName = form.DisplayName;
            return View("Profile", model);
        }
    }

    // --- E-posta / telefon degisikligi -----------------------------------------------------------------------

    [HttpGet("/ui/account/email")]
    public Task<IActionResult> Email([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
        => ContactPage(ContactKind.Email, clientKey, returnUrl, ct);

    [HttpGet("/ui/account/phone")]
    public Task<IActionResult> Phone([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
        => ContactPage(ContactKind.Phone, clientKey, returnUrl, ct);

    [HttpPost("/ui/account/email")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EmailSubmit([FromForm] AccountForm form, CancellationToken ct)
        => ContactSubmit(ContactKind.Email, form, ct);

    [HttpPost("/ui/account/phone")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PhoneSubmit([FromForm] AccountForm form, CancellationToken ct)
        => ContactSubmit(ContactKind.Phone, form, ct);

    [HttpGet("/ui/account/email/verify")]
    public Task<IActionResult> EmailVerify([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
        => ContactVerifyPage(ContactKind.Email, clientKey, returnUrl, ct);

    [HttpGet("/ui/account/phone/verify")]
    public Task<IActionResult> PhoneVerify([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
        => ContactVerifyPage(ContactKind.Phone, clientKey, returnUrl, ct);

    [HttpPost("/ui/account/email/verify")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EmailVerifySubmit([FromForm] AccountForm form, CancellationToken ct)
        => ContactVerifySubmit(ContactKind.Email, form, ct);

    [HttpPost("/ui/account/phone/verify")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PhoneVerifySubmit([FromForm] AccountForm form, CancellationToken ct)
        => ContactVerifySubmit(ContactKind.Phone, form, ct);

    private async Task<IActionResult> ContactPage(ContactKind kind, string? clientKey, string? returnUrl, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, Slug(kind), ct);
        if (ctx is null) return stop!;
        if (kind == ContactKind.Email && ctx.DirectoryManaged) return RedirectToIndex(clientKey!, returnUrl);

        var model = Model(ctx, clientKey!, returnUrl);
        model.Kind = kind;
        return View("Contact", model);
    }

    private async Task<IActionResult> ContactSubmit(ContactKind kind, AccountForm form, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(form.ClientId, form.ReturnUrl, Slug(kind), ct);
        if (ctx is null) return stop!;

        try
        {
            await accounts.StartContactChangeAsync(ctx, kind, form.Value, form.CurrentPassword, ct);
            return Redirect(uiOptions.GetAccountUrl($"{Slug(kind)}/verify", Query(form.ClientId!, form.ReturnUrl)));
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            var model = Model(ctx, form.ClientId!, form.ReturnUrl);
            model.Kind = kind;
            model.Value = form.Value;
            return View("Contact", model);
        }
    }

    private async Task<IActionResult> ContactVerifyPage(ContactKind kind, string? clientKey, string? returnUrl, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, $"{Slug(kind)}/verify", ct);
        if (ctx is null) return stop!;

        var pending = await accounts.GetPendingChangeAsync(ctx, kind, ct);
        if (pending is null)
            return Redirect(uiOptions.GetAccountUrl(Slug(kind), Query(clientKey!, returnUrl)));

        var model = Model(ctx, clientKey!, returnUrl);
        model.Kind = kind;
        model.Value = pending.Value;
        return View("ContactVerify", model);
    }

    private async Task<IActionResult> ContactVerifySubmit(ContactKind kind, AccountForm form, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(form.ClientId, form.ReturnUrl, $"{Slug(kind)}/verify", ct);
        if (ctx is null) return stop!;

        try
        {
            await accounts.CompleteContactChangeAsync(ctx, kind, form.Code, ct);
            return RedirectToIndex(form.ClientId!, form.ReturnUrl, Slug(kind).TrimStart('/'));
        }
        catch (ValidationException ex)
        {
            var pending = await accounts.GetPendingChangeAsync(ctx, kind, ct);
            if (pending is null)
                return Redirect(uiOptions.GetAccountUrl(Slug(kind), Query(form.ClientId!, form.ReturnUrl)));

            await AddErrorsAsync(ex, ct);
            var model = Model(ctx, form.ClientId!, form.ReturnUrl);
            model.Kind = kind;
            model.Value = pending.Value;
            return View("ContactVerify", model);
        }
    }

    // --- Parola ----------------------------------------------------------------------------------------------

    [HttpGet("/ui/account/password")]
    public async Task<IActionResult> Password([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, "/password", ct);
        if (ctx is null) return stop!;
        if (!ctx.HasPassword || ctx.DirectoryManaged) return RedirectToIndex(clientKey!, returnUrl);

        SetPasswordPolicy(ctx.Client);
        return View(Model(ctx, clientKey!, returnUrl));
    }

    [HttpPost("/ui/account/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasswordSubmit([FromForm] AccountForm form, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(form.ClientId, form.ReturnUrl, "/password", ct);
        if (ctx is null) return stop!;

        try
        {
            await accounts.ChangePasswordAsync(ctx, form.CurrentPassword, form.NewPassword, form.NewPasswordConfirm, ct);
            return RedirectToIndex(form.ClientId!, form.ReturnUrl, "password");
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            SetPasswordPolicy(ctx.Client);
            return View("Password", Model(ctx, form.ClientId!, form.ReturnUrl));
        }
    }

    // --- Oturumlar -------------------------------------------------------------------------------------------

    [HttpGet("/ui/account/sessions")]
    public async Task<IActionResult> Sessions([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, "/sessions", ct);
        if (ctx is null) return stop!;

        var model = Model(ctx, clientKey!, returnUrl);
        model.Sessions = await accounts.ListSessionsAsync(ctx, ct);
        return View(model);
    }

    [HttpPost("/ui/account/sessions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SessionsSubmit([FromForm] AccountForm form, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(form.ClientId, form.ReturnUrl, "/sessions", ct);
        if (ctx is null) return stop!;

        await accounts.SignOutOtherSessionsAsync(ctx, ct);
        return RedirectToIndex(form.ClientId!, form.ReturnUrl, "sessions");
    }

    // --- Hesabi sil ------------------------------------------------------------------------------------------

    [HttpGet("/ui/account/delete")]
    public async Task<IActionResult> Delete([FromQuery(Name = "client_id")] string? clientKey,
        [FromQuery(Name = "return_url")] string? returnUrl, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(clientKey, returnUrl, "/delete", ct);
        if (ctx is null) return stop!;
        if (!AccountService.CanDelete(ctx)) return RedirectToIndex(clientKey!, returnUrl);

        return View(Model(ctx, clientKey!, returnUrl));
    }

    [HttpPost("/ui/account/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubmit([FromForm] AccountForm form, CancellationToken ct)
    {
        var (ctx, stop) = await BeginAsync(form.ClientId, form.ReturnUrl, "/delete", ct);
        if (ctx is null) return stop!;

        try
        {
            await accounts.DeleteAccountAsync(ctx, form.CurrentPassword, form.Confirm, ct);
        }
        catch (ValidationException ex)
        {
            await AddErrorsAsync(ex, ct);
            return View("Delete", Model(ctx, form.ClientId!, form.ReturnUrl));
        }

        cookieManager.Clear(Response);
        var done = Model(ctx, form.ClientId!, form.ReturnUrl);
        return View("Deleted", done);
    }

    // --- Ortak -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Uygulamayi ve donus adresini dogrular, oturumu cozer. Oturum yoksa giris ekranina yonlendirir; giristen sonra
    /// <paramref name="page"/> sayfasina donulur.
    /// </summary>
    private async Task<(AccountContext? Ctx, IActionResult? Stop)> BeginAsync(string? clientKey, string? returnUrl,
        string page, CancellationToken ct)
    {
        var client = await accounts.FindClientAsync(clientKey, ct);
        if (client is null)
            return (null, Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.AccountUnavailable)));
        if (!AccountService.IsAllowedReturnUrl(client, returnUrl))
            return (null, Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidRedirectUri)));

        SetClientLogo(client);

        var ctx = await accounts.ResolveAsync(client, cookieManager.Get(Request), ct);
        if (ctx is not null)
            return (ctx, null);

        // Giris yalnizca yetkilendirme kodu akisi olan ve yonlendirme adresi kayitli uygulamalarda mumkun.
        if (client.RedirectUris.Count == 0 || !client.AllowedGrantTypes.HasFlag(OAuthGrantType.AuthorizationCode))
            return (null, Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.AccountUnavailable)));

        // POST sirasinda oturum dustuyse form verisi tasinmaz; giristen sonra sayfanin bos hali acilir.
        var key = await interactionStore.StoreAsync(new InteractionStateData
        {
            ClientKey = client.ClientKey,
            RedirectUri = client.RedirectUris[0],
            Scope = "openid",
            CodeChallenge = "-",
            AccountReturnUrl = uiOptions.GetAccountUrl(page, Query(client.ClientKey, returnUrl))
        }, LoginInteractionTtl, ct);

        return (null, Redirect(uiOptions.GetLoginUrl(key)));
    }

    private static AccountPageModel Model(AccountContext ctx, string clientKey, string? returnUrl)
    {
        var client = ctx.Client;
        var p = ctx.Principal;
        return new AccountPageModel
        {
            ClientId = clientKey,
            ReturnUrl = string.IsNullOrEmpty(returnUrl) ? null : returnUrl,
            Query = Query(clientKey, returnUrl),
            DisplayName = p.DisplayName,
            UserName = p.UserName,
            Email = p.Email,
            EmailVerified = p.EmailVerified,
            Phone = p.PhoneNumber,
            PhoneVerified = p.PhoneVerified,
            HasPassword = ctx.HasPassword,
            DirectoryManaged = ctx.DirectoryManaged,
            CanDelete = AccountService.CanDelete(ctx),
            // Telefon satiri yalnizca havuz telefonu bir sekilde kullaniyorsa (kimlik, SMS ile iki adim) ya da
            // kullanicinin zaten telefonu varsa; aksi halde SMS altyapisi olmayan kurulumda kod hic gelmezdi.
            ShowPhone = p.PhoneNumber is not null
                        || client.Login.AllowedIdentifiers.Contains(IdentifierType.Phone)
                        || client.Mfa.AllowedMethods.HasFlag(MfaMethod.Sms),
        };
    }

    private IActionResult RedirectToIndex(string clientKey, string? returnUrl, string? done = null)
    {
        var query = Query(clientKey, returnUrl);
        if (!string.IsNullOrEmpty(done))
            query += $"&done={Uri.EscapeDataString(done)}";
        return Redirect(uiOptions.GetAccountUrl(string.Empty, query));
    }

    private static string Slug(ContactKind kind) => kind == ContactKind.Email ? "/email" : "/phone";

    private static string Query(string clientKey, string? returnUrl)
        => string.IsNullOrEmpty(returnUrl)
            ? $"client_id={Uri.EscapeDataString(clientKey)}"
            : $"client_id={Uri.EscapeDataString(clientKey)}&return_url={Uri.EscapeDataString(returnUrl)}";

    private async Task AddErrorsAsync(ValidationException ex, CancellationToken ct)
    {
        var lang = ResolveLang();
        foreach (var error in ex.Errors)
            ModelState.AddModelError(error.PropertyName, await translationManager.GetAsync(error.ErrorMessage, lang, ct));
    }
}

/// <summary>Hesabim formlarinin ortak alanlari (her form yalnizca kendi alanlarini doldurur).</summary>
public sealed class AccountForm
{
    public string? ClientId { get; set; }
    public string? ReturnUrl { get; set; }
    public string? DisplayName { get; set; }
    public string? Value { get; set; }
    public string? Code { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? NewPasswordConfirm { get; set; }
    public bool Confirm { get; set; }
}

public sealed class AccountPageModel
{
    public required string ClientId { get; init; }
    public string? ReturnUrl { get; init; }
    public required string Query { get; init; }

    public string? DisplayName { get; set; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public bool EmailVerified { get; init; }
    public string? Phone { get; init; }
    public bool PhoneVerified { get; init; }
    public bool HasPassword { get; init; }
    public bool DirectoryManaged { get; init; }
    public bool CanDelete { get; init; }
    public bool ShowPhone { get; init; }

    public string? Notice { get; set; }
    public int ActiveSessionCount { get; set; }
    public ContactKind Kind { get; set; }
    public string? Value { get; set; }
    public IReadOnlyList<AccountSessionItem> Sessions { get; set; } = [];
}

/// <summary>Hesabim formlarindaki tek alan (bkz. Views/Account/_Field.cshtml).</summary>
public sealed record AccountFieldModel(
    string Name,
    string Label,
    string Type = "text",
    string? Autocomplete = null,
    string? Value = null,
    string? Hint = null,
    bool Autofocus = false,
    string? InputMode = null);
