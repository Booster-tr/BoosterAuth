using Booster.Auth.Database.Models;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Controllers;

/// <summary>
/// Izin (consent) ekrani: uygulamada "Onay iste" (RequireConsent) aciksa, kullanici istenen kapsamlara bir kez izin
/// verir; izin <see cref="Consent"/> tablosunda tutulur ve sonraki girislerde ayni kapsamlar icin tekrar sorulmaz
/// (<c>AuthorizeEndpoint.HasValidConsentAsync</c>). Izin panelden geri alinabilir.
/// <para>
/// Kapsam ve uygulama sorgu dizesinden DEGIL etkilesimden (interaction) okunur: adres elle degistirilerek baska bir
/// kapsam icin izin alinamaz. "allow" disindaki her POST ret sayilir.
/// </para>
/// </summary>
[AllowAnonymous]
public class ConsentController(
    IdpUiOptions uiOptions,
    ISessionCookieManager cookieManager,
    ISessionStore sessionStore,
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    ILocalUserStore userStore,
    IdpDbContext db,
    IRequestContextAccessor requestContextAccessor) : IdpBaseController
{
    [HttpGet("/ui/consent")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "interaction_key")] string? interactionKey, CancellationToken ct)
    {
        var (client, session, error) = await ResolveAsync(interactionKey, ct);
        if (error is not null)
            return error;

        var user = await userStore.GetByIdAsync(session!.PrincipalId, ct);

        return View(ConsentViewModel.Create(
            client!.ClientKey,
            client.DisplayName,
            Interaction!.Scope,
            interactionKey!,
            (string?)ViewData[ClientLogoUrlKey],
            user?.DisplayName,
            Interaction.RedirectUri));
    }

    [HttpPost("/ui/consent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        [FromForm(Name = "InteractionKey")] string? interactionKey,
        [FromForm(Name = "action")] string? action,
        CancellationToken ct)
    {
        var (client, session, error) = await ResolveAsync(interactionKey, ct);
        if (error is not null)
            return error;

        var interaction = Interaction!;

        if (!string.Equals(action, "allow", StringComparison.Ordinal))
        {
            // Reddedildi: uygulamaya standart hata ile donulur (OAuth 2.0 access_denied); etkilesim kapanir.
            await interactionStore.RemoveAsync(interactionKey!, ct);
            return Redirect(AppendQuery(interaction.RedirectUri, "error=access_denied", interaction.State));
        }

        using (requestContextAccessor.BeginSystemContext(client!.IsolationId))
        {
            var consent = await db.Consents.FirstOrDefaultAsync(
                c => c.PrincipalId == session!.PrincipalId && c.ClientId == client.ClientId, ct);

            if (consent is null)
            {
                db.Consents.Add(new Consent
                {
                    Id = EntityId.New(),
                    PrincipalId = session!.PrincipalId,
                    ClientId = client.ClientId,
                    GrantedScopes = interaction.Scope
                });
            }
            else
            {
                // Onceki izin + yeni istenenler: uygulama yeni bir kapsam istediginde yalnizca o eklenir.
                consent.GrantedScopes = string.Join(' ', consent.GrantedScopes
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Union(interaction.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal));
                consent.RevokedAt = null;
                consent.ExpiresAt = null;
            }

            await db.SaveChangesAsync(ct);
        }

        return Redirect(uiOptions.GetAuthorizeUrl(interactionKey!));
    }

    private async Task<(ClientData? Client, Session? Session, IActionResult? Error)> ResolveAsync(
        string? interactionKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return (null, null, Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction)));

        var client = await ResolveClientAsync(interactionStore, clientDataStore, interactionKey, ct);
        if (client is null || Interaction is null)
            return (null, null, Redirect(uiOptions.GetErrorUrl(IdpMessageKeys.InvalidInteraction)));

        var token = cookieManager.Get(Request);
        var session = string.IsNullOrEmpty(token) ? null : await sessionStore.ResolveAsync(token, ct);

        // Oturum yoksa (ya da baska havuzun oturumu) once giris: giristen sonra akis bu ekrana geri gelir.
        if (session is null || session.IsolationId != client.IsolationId)
            return (null, null, Redirect(uiOptions.GetLoginUrl(interactionKey)));

        return (client, session, null);
    }

    private static string AppendQuery(string redirectUri, string query, string? state)
    {
        if (!string.IsNullOrEmpty(state))
            query += $"&state={Uri.EscapeDataString(state)}";
        return redirectUri + (redirectUri.Contains('?') ? "&" : "?") + query;
    }
}
