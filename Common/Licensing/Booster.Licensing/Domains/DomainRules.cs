using System.Globalization;
using System.Net;

namespace Booster.Licensing.Domains;

public enum DomainCheck
{
    /// <summary>Lisanstaki bir domain'in kendisi veya alt alani.</summary>
    Allowed = 1,
    /// <summary>localhost, *.local, *.internal veya IP adresi. Her zaman serbest; panelde "test ortami" etiketiyle gosterilir.</summary>
    TestEnvironment = 2,
    NotAllowed = 3,
}

/// <summary>
/// Domain kontrolu. Lisanstaki domain bir ana domaindir (or. firma.com); host o domainin kendisi ya da alt alani ise
/// gecerlidir (auth.firma.com, pool1.firma.com). Public suffix listesine gerek yoktur, cunku eslesme yalnizca
/// "kendisi veya .ile biten" kuralina gore yapilir. "com.tr" gibi ciplak bir sonekin lisansa girmesini lisans portali
/// uretim aninda engeller.
/// Kontrol yalnizca YAPILANDIRMA aninda uyari icin kullanilir; calisan girisleri kesmez.
/// </summary>
public static class DomainRules
{
    private static readonly IdnMapping Idn = new();

    public static DomainCheck Check(string hostOrUrl, IEnumerable<string> allowedDomains)
    {
        var host = NormalizeHost(hostOrUrl);
        if (host is null) return DomainCheck.NotAllowed;
        if (IsTestHost(host)) return DomainCheck.TestEnvironment;

        foreach (var raw in allowedDomains)
        {
            var domain = NormalizeHost(raw);
            if (domain is null) continue;
            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal)) return DomainCheck.Allowed;
        }
        return DomainCheck.NotAllowed;
    }

    public static bool IsTestHost(string host)
    {
        var h = NormalizeHost(host);
        if (h is null) return false;
        return h == "localhost"
               || h.EndsWith(".localhost", StringComparison.Ordinal)
               || h.EndsWith(".local", StringComparison.Ordinal)
               || h.EndsWith(".internal", StringComparison.Ordinal)
               || IPAddress.TryParse(h.Trim('[', ']'), out _);
    }

    /// <summary>
    /// "https://Auth.Firma.com:8443/idp", "auth.firma.com." veya "auth.firma.com" degerlerinin hepsi "auth.firma.com"
    /// olur. IDN'ler ASCII (punycode) bicimine cevrilir. Gecersiz girdi null doner.
    /// </summary>
    public static string? NormalizeHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim();

        if (s.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)) return null;
            s = uri.Host;
        }
        else
        {
            var cut = s.IndexOfAny(['/', '?', '#']);
            if (cut >= 0) s = s[..cut];
            // host:port bicimindeki portu at. IPv6 koseli parantezli yazilir ([::1]:8080); onlara dokunulmaz.
            var colon = s.LastIndexOf(':');
            if (colon > 0 && !s.StartsWith('[') && s.IndexOf(':') == colon) s = s[..colon];
        }

        s = s.TrimEnd('.').ToLowerInvariant();
        if (s.Length == 0) return null;
        if (IPAddress.TryParse(s.Trim('[', ']'), out _)) return s;
        try { return Idn.GetAscii(s); }
        catch (ArgumentException) { return null; }
    }
}
