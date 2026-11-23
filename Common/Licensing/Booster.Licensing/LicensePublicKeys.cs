using System.Buffers.Text;

namespace Booster.Licensing;

/// <summary>
/// Urunun tanidigi lisans imza anahtarlari (kid -> Ed25519 public key, base64url).
/// KODA GOMULUDUR: config veya env ile eklenemez ya da degistirilemez. Aksi halde musteri kendi anahtarini
/// tanitip kendi lisansini uretebilirdi.
/// Rotasyon: yeni kid eklenir, eski kid en az bir surum boyunca listede kalir (eski lisanslar gecerli kalsin diye).
/// Sizan bir kid listeden cikarilir; o kid ile imzalanmis lisanslar o surumden itibaren Invalid olur.
/// Anahtar lisans portalinda (Services/Booster.Licensing.Portal) ilk acilista uretilir; public key oradan alinip buraya eklenir.
/// </summary>
public static class LicensePublicKeys
{
    private static readonly Dictionary<string, string> Keys = new(StringComparer.Ordinal)
    {
        // ["k2026a"] = "<portalin Anahtar sayfasindaki public key>",
    };

    public static IReadOnlyDictionary<string, byte[]> All { get; } =
        Keys.ToDictionary(k => k.Key, k => Base64Url.DecodeFromChars(k.Value), StringComparer.Ordinal);
}
