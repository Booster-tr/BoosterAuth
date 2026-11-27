namespace Booster.Labs.Site;

/// <summary>
/// Site ayarlari. PublicUrl canonical / hreflang / sitemap adreslerinde kullanilir (SEO).
/// Dagitimda SiteOptions__PublicUrl ile verilir; verilmezse yerel varsayilan.
/// </summary>
public sealed class SiteOptions
{
    public string PublicUrl { get; set; } = "https://www.boosterslabs.com";

    /// <summary>Iletisim formu / footer icin e-posta (gizli degil).</summary>
    public string ContactEmail { get; set; } = "info@boosterslabs.com";
}
