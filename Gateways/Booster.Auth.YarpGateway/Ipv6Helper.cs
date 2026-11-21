using System.Net;

namespace Booster.Auth.YarpGateway;

/// <summary>
/// Rate limit icin istemcinin gercek IP adresini bulur (IPv4 ve IPv6).
/// <para>
/// Gateway bir proxy (Cloudflare tuneli) arkasinda calisir; baglanti adresi
/// proxy'nin adresidir. Bu yuzden once proxy'nin ekledigi basliklara bakilir:
/// CF-Connecting-IP -> X-Forwarded-For (ilk adres) -> Forwarded (for=).
/// </para>
/// <para>
/// Onceki surum yalnizca IPv6 adresleri kabul ediyordu; IPv4 ile gelen tum
/// istemciler ayni "unknown" anahtarina dusup tek bir kotayi paylasiyordu.
/// </para>
/// </summary>
public static class Ipv6Helper
{
    public static string Get(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cf)
            && IPAddress.TryParse(cf.ToString().AsSpan().Trim(), out var cfIp))
            return cfIp.ToString();

        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xff))
        {
            var xffSpan = xff.ToString().AsSpan();

            // Ilk adres istemcidir; sonrakiler aradaki proxy'ler.
            foreach (Range range in xffSpan.Split(','))
            {
                if (IPAddress.TryParse(xffSpan[range].Trim(), out var ip))
                    return ip.ToString();
            }
        }

        if (ctx.Request.Headers.TryGetValue("Forwarded", out var fwd))
        {
            var fwdSpan = fwd.ToString().AsSpan();

            foreach (Range range in fwdSpan.Split(','))
            {
                var segSpan = fwdSpan[range].Trim();

                var forIdx = segSpan.IndexOf("for=", StringComparison.OrdinalIgnoreCase);
                if (forIdx < 0) continue;

                var tokenSpan = segSpan[(forIdx + 4)..];

                var semiIdx = tokenSpan.IndexOf(';');
                if (semiIdx >= 0)
                    tokenSpan = tokenSpan[..semiIdx];

                tokenSpan = tokenSpan.Trim().Trim('"').Trim('[').Trim(']');

                if (IPAddress.TryParse(tokenSpan, out var ip))
                    return ip.ToString();
            }
        }

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
