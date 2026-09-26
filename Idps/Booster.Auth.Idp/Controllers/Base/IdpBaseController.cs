using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Caching.Abstraction;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers.Base;

public class IdpBaseController : Controller
{
    protected string ResolveLang()
    {
        var lang = Request.Headers.AcceptLanguage.FirstOrDefault() ?? "tr-tr";
        return lang.Length > 5 ? lang[..5].ToLowerInvariant() : lang.ToLowerInvariant();
    }

    protected async Task<ClientData?> ResolveClientAsync(
        ISecureCacheStore<InteractionStateData> interactionStore,
        IClientDataStore clientDataStore,
        string interactionKey,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interactionKey))
            return null;

        var interaction = await interactionStore.GetAsync(interactionKey, ct);
        if (interaction is null)
            return null;

        return await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct);
    }

    protected void SetPasswordlessMethods(ClientData? client)
    {
        var methods = new List<PasswordlessMethod>(2);

        if (client is not null)
        {

            if (client.Login.AllowedLoginProviders.HasFlag(LoginProviderType.EmailCode)
                && client.Login.AllowedIdentifiers.Contains(IdentifierType.Email))
            {
                methods.Add(new PasswordlessMethod("email_code", "Login.Passwordless.Email"));
            }

            if (client.Login.AllowedLoginProviders.HasFlag(LoginProviderType.PhoneCode)
                && client.Login.AllowedIdentifiers.Contains(IdentifierType.Phone))
            {
                methods.Add(new PasswordlessMethod("phone_code", "Login.Passwordless.Phone"));
            }
        }

        ViewData["PasswordlessMethods"] = methods;
    }

    protected void SetFederationMethods(ClientData? client)
    {
        var methods = client is null
            || !client.Login.AllowedLoginProviders.HasFlag(LoginProviderType.Federation)
            ? []
            : client.FederationProviders
                .Select(p => new FederationMethod(p.Key, p.ProviderType, p.DisplayName))
                .ToList();

        ViewData["FederationMethods"] = methods;
    }

    /// <summary>
    /// Parola kurallarini goruntuye tasir.
    /// <para>
    /// Yedek varsayilan YOK: istemci cozulemediyse null kalir ve ekran kural
    /// gostermez. Uydurma bir varsayilan koymak, havuz politikasi yaninda ikinci
    /// bir dogruluk kaynagi yaratir ve politika degistiginde sessizce yanlisa
    /// donerdi.
    /// </para>
    /// </summary>
    protected void SetPasswordPolicy(ClientData? client)
        => ViewData["PasswordPolicy"] = client?.Password;

    /// <summary>
    /// Kayit formunda hangi kimlik alanlarinin tarayici tarafindan zorunlu
    /// tutulacagini bildirir. Havuz politikasindan okunur; sabit yazilsaydi
    /// e-postanin istege bagli oldugu bir havuzda form gereksiz yere bloke
    /// edilirdi.
    /// </summary>
    protected void SetRegistrationRequirements(ClientData? client)
    {
        var allowed = client?.Login.AllowedIdentifiers;

        ViewData["EmailRequired"] = allowed?.Contains(IdentifierType.Email) ?? false;
        ViewData["UserNameRequired"] = allowed?.Contains(IdentifierType.Username) ?? false;
    }

    protected void SetIdentifierField(ClientData? client)
        => ViewData["IdentifierField"] = client is null
            ? IdentifierField.From([IdentifierType.Email])
            : IdentifierField.From(client.Login.AllowedIdentifiers);
}
