using Booster.Auth.Manager.RequestContexts;
using Booster.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Booster.Auth.Common.Internal.Auditing;

/// <summary>
/// Denetim kaydi: guvenlik ve kisisel veri isleme olaylari (kim, ne zaman, nereden, neyi, sonuc). Ayri akisa gider ve
/// isletim loglarindan uzun saklanir (bkz. dokuman: Isletim &gt; Loglar ve denetim kayitlari).
/// <para>
/// KVKK veri minimizasyonu: kayitta e-posta, telefon, ad, parola, kod, token YOKTUR. Kisi kimligi (sub) ve havuz /
/// uygulama kimlikleri yazilir; bilinmeyen kullaniciyla basarisiz giriste girilen kimlik yalnizca ozet (hash) olarak.
/// IP adresi ve tarayici bilgisi guvenlik incelemesi icin tutulur (KVKK m.12 veri guvenligi tedbiri).
/// </para>
/// </summary>
public sealed class AuditLog(ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor, IRequestContextAccessor requestContextAccessor)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(AuditCategory.Name);

    public void Success(string action, AuditEntry entry) => Write(action, "success", entry);

    public void Failure(string action, AuditEntry entry) => Write(action, "failure", entry);

    private void Write(string action, string outcome, AuditEntry entry)
    {
        var http = httpContextAccessor.HttpContext;
        var context = requestContextAccessor.Context();

        // Panelden yapilan islemde "yapan" token sahibidir; son kullanici akisinda kendisidir.
        var actor = entry.ActorId ?? (context.PrincipalId != Guid.Empty ? context.PrincipalId : (Guid?)null);
        var userAgent = http?.Request.Headers.UserAgent.ToString();
        if (userAgent is { Length: > 200 })
            userAgent = userAgent[..200];

        // Alanlar yapisal (OTLP ozniteligi): arayuzde Action, Outcome, PrincipalId ... ile filtrelenir.
        _logger.LogInformation(
            "Denetim {Action} {Outcome} reason={Reason} principal={PrincipalId} actor={ActorId} pool={PoolId} client={ClientId} " +
            "target={Target} method={Method} identifier={IdentifierHash} ip={IpAddress} ua={UserAgent}",
            action, outcome, entry.Reason, entry.PrincipalId, actor, entry.PoolId, entry.ClientId,
            entry.Target, entry.Method, entry.IdentifierHash, http?.Connection.RemoteIpAddress?.ToString(), userAgent);
    }
}

/// <param name="PrincipalId">Islemin ilgili oldugu kisi (sub).</param>
/// <param name="ActorId">Islemi yapan (verilmezse istegin token sahibi).</param>
/// <param name="Reason">Basarisizlik nedeni (mesaj anahtari) ya da kisa aciklama; kisisel veri icermez.</param>
/// <param name="Target">Islem hedefi (or. "role:&lt;id&gt;", "PrincipalId=&lt;id&gt;"); yalnizca kimlikler.</param>
/// <param name="Method">Giris yontemi (local, ldap, email_code, google ...).</param>
/// <param name="IdentifierHash">Bilinmeyen kullaniciyla denemede girilen kimligin ozeti (duz metin yazilmaz).</param>
public sealed record AuditEntry(
    Guid? PrincipalId = null,
    Guid? PoolId = null,
    Guid? ClientId = null,
    string? Reason = null,
    string? Target = null,
    Guid? ActorId = null,
    string? Method = null,
    string? IdentifierHash = null);
