using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Musteri kurulum paketi (repo: deploy/compose/{tr|en}). Dosyalar portala gomulu kaynak olarak derlenir; zip dil
/// basina ilk istekte uretilip bellekte tutulur. Sitenin dili hangisiyse paket (betikler, README, .env.example,
/// yorumlar) o dildedir. Zip icinde tum dosyalar "booster-auth/" klasorundedir; .sh dosyalari 0755 yazilir.
/// </summary>
public sealed partial class InstallPackage
{
    public const string FileName = "booster-auth.zip";
    public static readonly IReadOnlyList<string> Languages = ["tr", "en"];
    private const string Prefix = "pkg/";
    private const string Root = "booster-auth/";
    private const string ComposeFile = "docker-compose.yml";

    private readonly ConcurrentDictionary<string, Lazy<byte[]>> _zips = new(StringComparer.Ordinal);

    public byte[] Zip(string lang) => _zips.GetOrAdd(Normalize(lang), l => new Lazy<byte[]>(() => Build(l))).Value;

    /// <summary>Paketteki dosya adlari (indirme sayfasinda gosterilir).</summary>
    public static IReadOnlyList<string> Files(string lang)
        => Resources(Normalize(lang)).Select(r => r.Path).Order(StringComparer.Ordinal).ToList();

    private static string Normalize(string lang) => Languages.Contains(lang) ? lang : "en";

    private static IEnumerable<(string Resource, string Path)> Resources(string lang)
    {
        var prefix = $"{Prefix}{lang}/";
        // Glob ve acik .env.example ayni dosyayi iki kez ekleyebilir; yol bazinda tekillestirilir.
        return typeof(InstallPackage).Assembly.GetManifestResourceNames()
            .Select(n => (Resource: n, Normalized: n.Replace('\\', '/')))
            .Where(n => n.Normalized.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => (n.Resource, Path: n.Normalized[prefix.Length..]))
            .DistinctBy(r => r.Path, StringComparer.Ordinal);
    }

    private static byte[] Build(string lang)
    {
        EnsureSameCompose();

        var asm = typeof(InstallPackage).Assembly;
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (resource, path) in Resources(lang).OrderBy(r => r.Path, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(Root + path, CompressionLevel.Optimal);
                // Unix izinleri ust 16 bitte: normal dosya (0100000) + 0755 / 0644.
                var mode = path.EndsWith(".sh", StringComparison.Ordinal) ? 0x81ED : 0x81A4;
                entry.ExternalAttributes = mode << 16;
                using var src = asm.GetManifestResourceStream(resource)!;
                using var dst = entry.Open();
                src.CopyTo(dst);
            }
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Dil kopyalari yalnizca YORUMDA farkli olabilir. Biri guncellenip digeri unutulursa paket uretilmez;
    /// aksi halde iki dildeki musteri farkli bir sistem kurardi.
    /// </summary>
    private static void EnsureSameCompose()
    {
        var versions = Languages.Select(l => ReadWithoutComments(l, ComposeFile)).ToList();
        if (versions.Any(v => !v.SequenceEqual(versions[0], StringComparer.Ordinal)))
            throw new InvalidOperationException($"deploy/compose/*/{ComposeFile} dil kopyalari yorum disinda farkli.");
    }

    private static List<string> ReadWithoutComments(string lang, string path)
    {
        var resource = Resources(lang).First(r => r.Path == path).Resource;
        using var reader = new StreamReader(typeof(InstallPackage).Assembly.GetManifestResourceStream(resource)!);
        // Zorunlu degisken mesajlari (${X:?mesaj}) dile gore farkli olabilir: Ingilizce pakette Turkce hata cikmasin.
        return reader.ReadToEnd().Replace("\r", "").Split('\n')
            .Where(l => !l.TrimStart().StartsWith('#'))
            .Select(l => RequiredMessage().Replace(l, "${$1:?}"))
            .ToList();
    }

    [GeneratedRegex(@"\$\{(\w+):\?[^}]*\}")]
    private static partial Regex RequiredMessage();
}
