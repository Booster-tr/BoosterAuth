using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Booster.Auth.YarpGateway;

/// <summary>
/// Istemcinin gercek IP'si. Basliklar (<c>CF-Connecting-IP</c>, <c>X-Forwarded-For</c>) YALNIZCA baglanti guvenilen bir
/// proxy'den geliyorsa okunur. Onceki surum basliga her zaman guveniyordu: istemci her istekte farkli bir
/// <c>X-Forwarded-For</c> gondererek IP basina hiz sinirini atlayabiliyor, oturumlara sahte IP yazdirabiliyordu.
/// <para>
/// Varsayilan: hicbir proxy'ye guvenilmez, baglanti adresi kullanilir (musteri kurulumunda gateway disa acik ucun
/// kendisidir). Onunde yuk dengeleyici / Cloudflare varsa:
/// <c>ClientIp__TrustedNetworks__0=10.0.0.0/8</c> ve gerekiyorsa <c>ClientIp__Header=CF-Connecting-IP</c>.
/// </para>
/// Bulunan adres <c>Connection.RemoteIpAddress</c>'e yazilir: hiz siniri bu adrese bakar, YARP da arka servislere
/// <c>X-Forwarded-For</c> olarak bu adresi (istemcinin gonderdigini degil) iletir.
/// </summary>
public sealed class ClientIpOptions
{
    /// <summary>Guvenilen proxy aglari (CIDR). Bos = hicbir basliga guvenilmez.</summary>
    public List<string> TrustedNetworks { get; set; } = [];

    /// <summary><c>X-Forwarded-For</c> (varsayilan) ya da <c>CF-Connecting-IP</c>.</summary>
    public string Header { get; set; } = ForwardedHeadersDefaults.XForwardedForHeaderName;
}

public sealed class ClientIpResolver
{
    private readonly IReadOnlyList<IPNetwork> _trusted;
    private readonly string _header;

    public ClientIpResolver(ClientIpOptions options)
    {
        // Bos deger (compose: TRUSTED_PROXY_NETWORK verilmedi) yok sayilir.
        _trusted = options.TrustedNetworks.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => IPNetwork.Parse(n.Trim())).ToList();
        _header = options.Header;
    }

    public IPAddress? Resolve(HttpContext ctx)
    {
        var remote = Normalize(ctx.Connection.RemoteIpAddress);
        if (remote is null || !IsTrusted(remote) || !ctx.Request.Headers.TryGetValue(_header, out var value))
            return remote;

        // X-Forwarded-For: sagdan sola, guvenilen proxy'leri atlayip ilk guvenilmeyen adres istemcidir.
        // Soldaki adresleri istemci kendisi yazabilir; onlara bakilmaz.
        var candidates = value.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = candidates.Length - 1; i >= 0; i--)
        {
            if (!IPAddress.TryParse(candidates[i], out var ip))
                return remote;

            ip = Normalize(ip)!;
            if (i == 0 || !IsTrusted(ip))
                return ip;
        }

        return remote;
    }

    private bool IsTrusted(IPAddress ip) => _trusted.Any(n => n.Contains(ip));

    private static IPAddress? Normalize(IPAddress? ip) => ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip;
}
