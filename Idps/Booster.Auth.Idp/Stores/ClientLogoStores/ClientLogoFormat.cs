namespace Booster.Auth.Idp.Stores.ClientLogoStores;

/// <summary>
/// Kabul edilen logo biçimleri. Biçim dosya adından ya da istemcinin bildirdiği
/// Content-Type'tan değil, dosyanın ilk baytlarından çıkarılır.
/// <para>
/// SVG bilinçli olarak YOK: IdP kökenden servis edilen bir SVG betik
/// çalıştırabilir; giriş ekranlarının kökeninde bu kabul edilemez.
/// </para>
/// </summary>
public sealed record ClientLogoFormat(string Extension, string ContentType)
{
    public static readonly ClientLogoFormat Png = new("png", "image/png");
    public static readonly ClientLogoFormat Jpeg = new("jpg", "image/jpeg");
    public static readonly ClientLogoFormat Gif = new("gif", "image/gif");
    public static readonly ClientLogoFormat Webp = new("webp", "image/webp");

    public static IReadOnlyList<ClientLogoFormat> All { get; } = [Png, Jpeg, Gif, Webp];

    /// <summary>Dosya imzasına bakarak biçimi bulur; tanınmazsa null.</summary>
    public static ClientLogoFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return Png;

        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return Jpeg;

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
            return Gif;

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return Webp;

        return null;
    }
}
