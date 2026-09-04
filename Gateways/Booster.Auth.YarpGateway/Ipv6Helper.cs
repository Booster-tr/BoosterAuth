using System.Net;
using System.Net.Sockets;

namespace Booster.Auth.YarpGateway;

public static class Ipv6Helper
{
    public static string Get(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cf))
        {
            var cfSpan = cf.ToString().AsSpan().Trim();
            if (IPAddress.TryParse(cfSpan, out var cfIp) && cfIp.AddressFamily == AddressFamily.InterNetworkV6)
                return cfIp.ToString();
        }

        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xff))
        {
            var xffSpan = xff.ToString().AsSpan();

            foreach (Range range in xffSpan.Split(','))
            {
                var ipSpan = xffSpan[range].Trim();
                if (IPAddress.TryParse(ipSpan, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
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
                {
                    tokenSpan = tokenSpan[..semiIdx];
                }

                tokenSpan = tokenSpan.Trim().Trim('"').Trim('[').Trim(']');

                if (IPAddress.TryParse(tokenSpan, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
                    return ip.ToString();
            }
        }

        var rip = ctx.Connection.RemoteIpAddress;
        return rip?.AddressFamily == AddressFamily.InterNetworkV6 ? rip.ToString() : "unknown";
    }
}
