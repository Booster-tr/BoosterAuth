using System.Globalization;

namespace Booster.Auth.Idp.Managers.LegalManagers;

/// <summary>
/// Varsayilan yasal metin sablonlari: <c>LegalTemplates/{anahtar}.{dil}.md</c> (gomulu kaynak).
/// Dosya basi: "title:", "summary:", "date: yyyy-MM-dd" satirlari, ardindan "---" ve Markdown govde.
/// </summary>
public static class LegalTemplates
{
    public sealed record Template(string Title, string? Summary, DateOnly EffectiveDate, string Content);

    public static Template? Load(string key, string language)
    {
        var asm = typeof(LegalTemplates).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"LegalTemplates.{key}.{language}.md", StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return null;

        using var reader = new StreamReader(asm.GetManifestResourceStream(name)!);
        var text = reader.ReadToEnd().Replace("\r\n", "\n");

        var split = text.IndexOf("\n---\n", StringComparison.Ordinal);
        if (split < 0)
            throw new InvalidOperationException($"Yasal sablon basligi eksik: {name}");

        var header = text[..split].Split('\n')
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

        return new Template(
            header["title"],
            header.TryGetValue("summary", out var s) && s.Length > 0 ? s : null,
            DateOnly.ParseExact(header["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture),
            text[(split + 5)..].Trim() + "\n");
    }
}
