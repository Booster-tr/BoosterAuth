using Booster.Licensing.Domains;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Lisansa girecek domain listesini kullanici girdisinden cikarir ve dogrular.
/// Urun, host'u "domainin kendisi veya alt alani" kuraliyla esler. Bu yuzden lisansa ciplak bir sonek girerse
/// (or. "com.tr") o sonekin altindaki HER domain gecerli olurdu. Bu sinif bunu engeller.
/// </summary>
public static class DomainInput
{
    /// <summary>
    /// Tek basina domain olamayacak yaygin sonekler. Tam public suffix listesi degildir; en sik hatalari yakalar.
    /// Iki parcali bir girdinin ilk parcasi bunlardan biriyse (or. "com.tr", "co.uk") reddedilir.
    /// </summary>
    private static readonly HashSet<string> SecondLevelSuffixes = new(StringComparer.Ordinal)
    {
        "com", "net", "org", "gov", "edu", "mil", "int", "gen", "biz", "info", "name", "web", "tv",
        "bel", "pol", "tsk", "k12", "av", "dr", "bbs", "tel", "co", "ac", "or", "ne", "go", "nom", "ltd", "plc",
    };

    public static (List<string> Domains, List<string> Errors) Parse(string? text)
    {
        var domains = new List<string>();
        var errors = new List<string>();

        var parts = (text ?? string.Empty).Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            var host = DomainRules.NormalizeHost(raw);
            if (host is null) { errors.Add($"{raw}: gecerli bir domain degil."); continue; }
            if (DomainRules.IsTestHost(host)) { errors.Add($"{raw}: test ortami adresleri (localhost, *.local, *.internal, IP) lisansa yazilmaz; zaten her zaman serbesttir."); continue; }

            var labels = host.Split('.');
            if (labels.Length < 2 || labels.Any(l => l.Length == 0))
            { errors.Add($"{raw}: tam bir domain girin (or. firma.com)."); continue; }
            if (labels.Length == 2 && SecondLevelSuffixes.Contains(labels[0]))
            { errors.Add($"{raw}: bu bir uzanti; firmanin domainini girin (or. firma.{host})."); continue; }

            if (!domains.Contains(host)) domains.Add(host);
        }

        if (domains.Count == 0 && errors.Count == 0) errors.Add("En az bir domain girin.");
        return (domains, errors);
    }
}
