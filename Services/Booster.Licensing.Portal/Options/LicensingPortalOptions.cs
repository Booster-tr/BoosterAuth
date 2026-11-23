namespace Booster.Licensing.Portal.Options;

public sealed class LicensingPortalOptions
{
    /// <summary>Booster Auth issuer adresi, or. https://auth.appnuraymind.com/idp.</summary>
    public string Authority { get; set; } = default!;

    /// <summary>Booster Auth'taki portal client'i (LicensingBootstrapper olusturur).</summary>
    public string ClientId { get; set; } = "licensing-portal";

    /// <summary>
    /// Admin ekranlarina girebilen e-postalar. E-posta Booster Auth id_token'indan gelir; musteri havuzunda kayitli
    /// ve dogrulanmis olmalidir. Gizli bilgi degildir; ortam degiskeniyle verilir.
    /// </summary>
    public List<string> AdminEmails { get; set; } = [];

    /// <summary>Yalnizca lokal gelistirmede false (self-signed sertifika / http).</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && AdminEmails.Any(a => string.Equals(a.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
