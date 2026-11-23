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

    /// <summary>Portalin disaridan erisilen adresi; e-postalardaki baglantilar buna gore uretilir.</summary>
    public string PublicUrl { get; set; } = "https://licensing.appnuraymind.com";

    /// <summary>Havale/EFT talimati (gizli degil). Degerler girilmeden siparis ekraninda yer tutucu gorunur.</summary>
    public BankAccountOptions Bank { get; set; } = new();

    /// <summary>Yalnizca lokal gelistirmede false (self-signed sertifika / http).</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    public sealed class BankAccountOptions
    {
        public string BankName { get; set; } = "(banka adi girilmedi)";
        public string AccountName { get; set; } = "(alici adi girilmedi)";
        public string Iban { get; set; } = "(IBAN girilmedi)";
    }

    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && AdminEmails.Any(a => string.Equals(a.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
