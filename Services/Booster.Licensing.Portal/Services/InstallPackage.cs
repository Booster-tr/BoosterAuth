using System.IO.Compression;
using System.Reflection;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Musteri kurulum paketi (repo: deploy/compose). Dosyalar portala gomulu kaynak olarak derlenir; zip ilk istekte
/// uretilip bellekte tutulur. Zip icinde tum dosyalar "booster-auth/" klasorundedir; .sh dosyalari calistirilabilir
/// izinle (0755) yazilir, boylece Linux'ta "unzip" sonrasi ./kurulum.sh dogrudan calisir.
/// </summary>
public sealed class InstallPackage
{
    public const string FileName = "booster-auth.zip";
    private const string Prefix = "pkg/";
    private const string Root = "booster-auth/";

    private readonly Lazy<byte[]> _zip = new(Build);

    public byte[] Zip => _zip.Value;

    /// <summary>Paketteki dosya adlari (indirme sayfasinda gosterilir).</summary>
    public static IReadOnlyList<string> Files { get; } = Resources().Select(r => r.Path).Order(StringComparer.Ordinal).ToList();

    private static IEnumerable<(string Resource, string Path)> Resources()
    {
        var asm = typeof(InstallPackage).Assembly;
        // Glob ve acik .env.example ayni dosyayi iki kez ekleyebilir; yol bazinda tekillestirilir.
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(n => (Resource: n, Path: n[Prefix.Length..].Replace('\\', '/')))
            .DistinctBy(r => r.Path, StringComparer.Ordinal);
    }

    private static byte[] Build()
    {
        var asm = typeof(InstallPackage).Assembly;
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (resource, path) in Resources().OrderBy(r => r.Path, StringComparer.Ordinal))
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
}
