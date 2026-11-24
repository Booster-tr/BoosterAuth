using Booster.Auth.Common.Internal.Constants;
using Microsoft.AspNetCore.Http;

namespace Booster.Auth.Common.Internal.Auditing;

/// <summary>
/// Yonetim islemlerinin denetim kaydi, tek noktadan. Kaydedilenler:
/// <list type="bullet">
/// <item>Panel token'i (<c>booster:panel</c>) ya da servis hesabi token'i ile yapilan HER degisiklik (POST / PUT / PATCH /
/// DELETE): kullanici, rol, kapsam, uygulama, havuz, firma, yetki, lisans, yasal metin ...</item>
/// <item>Paneldeki kisisel veri OKUMALARI (<see cref="PersonalDataReadPrefixes"/>): KVKK geregi kisisel veriye kimin
/// eristigi izlenebilir olmali.</item>
/// </list>
/// Kayitta yol, yalnizca sorgudaki kimlik parametreleri (…Id) ve sonuc vardir; govde (kisisel veri icerebilir) yazilmaz.
/// </summary>
public sealed class AdminAuditMiddleware(RequestDelegate next, AuditLog audit)
{
    /// <summary>Kisisel veri donduren okuma uclari (IdP yolu; gateway /idp onekini kaldirir).</summary>
    private static readonly string[] PersonalDataReadPrefixes =
    [
        "/pools/users", "/pools/service-accounts", "/clients/sessions", "/clients/consents", "/principals", "/apis/users"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldAudit(context))
        {
            await next(context);
            return;
        }

        var failed = false;
        try
        {
            await next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            var status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            // Yapan: token sahibi (sub). Istek baglamina guvenilmez; her serviste ayni yerde kurulmuyor olabilir.
            Guid? actor = Guid.TryParse(context.User.FindFirst("sub")?.Value, out var sub) ? sub : null;
            Guid? actorPool = Guid.TryParse(context.User.FindFirst("actor")?.Value, out var pool) ? pool : null;
            var entry = new AuditEntry(Reason: status.ToString(), Target: Identifiers(context.Request.Query), ActorId: actor, PoolId: actorPool);
            var action = $"api.{context.Request.Method.ToLowerInvariant()} {context.Request.Path}";
            if (status < 400)
                audit.Success(action, entry);
            else
                audit.Failure(action, entry);
        }
    }

    private static bool ShouldAudit(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
            return false;

        var panel = (user.FindFirst("scope")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(AuthConstants.ManagementPanelScope, StringComparer.Ordinal);
        var machine = string.Equals(user.FindFirst("principal_type")?.Value, "Machine", StringComparison.OrdinalIgnoreCase);
        if (!panel && !machine)
            return false;

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method))
            return true;

        var path = context.Request.Path.Value ?? string.Empty;
        return PersonalDataReadPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Yalnizca kimlik parametreleri (adi "Id" ile biten ve Guid olan); arama metni vb. yazilmaz.</summary>
    private static string? Identifiers(IQueryCollection query)
    {
        var ids = query
            .Where(q => q.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(q.Value.ToString(), out _))
            .Select(q => $"{q.Key}={q.Value}")
            .ToList();
        return ids.Count == 0 ? null : string.Join(';', ids);
    }
}
