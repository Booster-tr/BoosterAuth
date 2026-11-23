using Booster.Licensing.Domains;

namespace Booster.Licensing.Portal.Services;

/// <summary>Self-servis kurallarinin sabitleri ve kucuk yardimcilari (deneme ve aktarim).</summary>
public static class SelfServiceRules
{
    public const int TrialRequestsPerIpPerDay = 3;
    public const int TransfersPerYear = 3;

    /// <summary>
    /// Deneme verilmeyen ucretsiz e-posta saglayicilari. Kurumsal deneme icin firmanin kendi domaini gerekir;
    /// aksi halde herkes sinirsiz deneme acabilirdi.
    /// </summary>
    public static readonly HashSet<string> FreeEmailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "hotmail.com", "hotmail.com.tr", "outlook.com", "outlook.com.tr", "live.com",
        "msn.com", "yahoo.com", "yahoo.com.tr", "ymail.com", "yandex.com", "yandex.com.tr", "yandex.ru", "ya.ru",
        "icloud.com", "me.com", "mac.com", "aol.com", "proton.me", "protonmail.com", "pm.me", "gmx.com", "gmx.net",
        "gmx.de", "mail.com", "mail.ru", "zoho.com", "tutanota.com", "tuta.io", "fastmail.com", "hey.com",
        "mynet.com", "windowslive.com",
    };

    public static string? EmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        return at < 0 ? null : DomainRules.NormalizeHost(email[(at + 1)..]);
    }

    /// <summary>
    /// E-posta domaini ile kurulum domaini "uyusur" mu: biri digerinin kendisi ya da alt alani.
    /// (ahmet@firma.com -&gt; firma.com, ahmet@mail.firma.com -&gt; firma.com, ahmet@firma.com -&gt; auth.firma.com).
    /// </summary>
    public static bool DomainsRelated(string a, string b) =>
        a == b || a.EndsWith("." + b, StringComparison.Ordinal) || b.EndsWith("." + a, StringComparison.Ordinal);
}
