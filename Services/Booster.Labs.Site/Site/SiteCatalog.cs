namespace Booster.Labs.Site;

/// <summary>
/// Sitenin sayfa kaydi, dilleri ve ortak (chrome) metinleri. Sayfa GOVDE metinleri dile ozel
/// gorunumlerdedir (Views/Site/{dil}/{View}.cshtml); burada yalnizca menu/footer/dugme gibi ortak
/// yazilar ve SEO basligi/aciklamasi tutulur. Yeni sayfa: buraya kayit + iki dilde gorunum.
/// </summary>
public static class SiteCatalog
{
    public const string CompanyName = "Boosters Labs";
    public const string DefaultLanguage = "tr";
    public static readonly string[] Languages = ["tr", "en"];

    public sealed record Page(string Slug, string View, (string Tr, string En) Title, (string Tr, string En) Desc)
    {
        public string SeoTitle(string lang) => lang == "tr" ? Title.Tr : Title.En;
        public string Description(string lang) => lang == "tr" ? Desc.Tr : Desc.En;
        public string Path(string lang) => Slug.Length == 0 ? $"/{lang}" : $"/{lang}/{Slug}";
    }

    public static readonly Page Home = new("", "home",
        ("Boosters Labs — Kimlik, yetki ve oyun teknolojileri",
         "Boosters Labs — Identity, authorization and game technology"),
        ("Boosters Labs; kurumsal kimlik ve yetkilendirme platformu Boosters Auth'un ve Block Boosters oyununun arkasindaki ekip. Guvenli, olceklenebilir, size ait.",
         "Boosters Labs is the team behind Boosters Auth, our enterprise identity and authorization platform, and the game Block Boosters. Secure, scalable, truly yours."));

    public static readonly Page Products = new("products", "products",
        ("Urunlerimiz", "Our Products"),
        ("Boosters Labs urunleri: cok kiracili kimlik ve yetki platformu Boosters Auth. Kurumsal guvenlik, on-prem ve cevrimdisi lisans.",
         "Boosters Labs products: Boosters Auth, a multi-tenant identity and authorization platform. Enterprise security, on-prem and offline licensing."));

    public static readonly Page BoostersAuth = new("products/boosters-auth", "boosters-auth",
        ("Boosters Auth — Kimlik ve Yetki Platformu", "Boosters Auth — Identity & Authorization Platform"),
        ("Boosters Auth: OIDC/OAuth2 kimlik saglayici ve OpenFGA tabanli yetki. Cok kiracili, on-prem, cevrimdisi lisansli. SSO, MFA, federasyon, LDAP.",
         "Boosters Auth: an OIDC/OAuth2 identity provider with OpenFGA authorization. Multi-tenant, on-prem, offline-licensed. SSO, MFA, federation, LDAP."));

    public static readonly Page Games = new("games", "games",
        ("Oyunlarimiz", "Our Games"),
        ("Boosters Labs oyunlari: Block Boosters. Boosters Labs'in oyun stududyosundan.",
         "Boosters Labs games: Block Boosters. From the Boosters Labs game studio."));

    public static readonly Page BlockBoosters = new("games/block-boosters", "block-boosters",
        ("Block Boosters", "Block Boosters"),
        ("Block Boosters: Boosters Labs'in bulmaca/aksiyon oyunu. Her yastan oyuncu icin tasarlandi.",
         "Block Boosters: a puzzle/action game by Boosters Labs. Designed for players of every age."));

    public static readonly Page About = new("about", "about",
        ("Hakkimizda", "About Us"),
        ("Boosters Labs; kimlik, yetki ve oyun teknolojileri gelistiren bir yazilim ekibi. Misyonumuz, vizyonumuz ve degerlerimiz.",
         "Boosters Labs is a software team building identity, authorization and game technology. Our mission, vision and values."));

    public static readonly Page Contact = new("contact", "contact",
        ("Iletisim", "Contact"),
        ("Boosters Labs ile iletisime gecin. Sorulariniz, is birligi ve destek talepleriniz icin buradayiz.",
         "Get in touch with Boosters Labs. We are here for your questions, partnerships and support requests."));

    public static readonly IReadOnlyList<Page> All = [Home, Products, BoostersAuth, Games, BlockBoosters, About, Contact];

    public static Page? Find(string slug) => All.FirstOrDefault(p => p.Slug == slug);

    /// <summary>Tarayici dili tr ise tr, degilse en (kok "/" yonlendirmesi).</summary>
    public static string PreferredLanguage(string? acceptLanguage) =>
        !string.IsNullOrEmpty(acceptLanguage) && acceptLanguage.TrimStart().StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            ? "tr" : "en";

    // ---------------------------------------------------------------- ortak (chrome) metinler
    public static string T(string lang, string key) =>
        (lang == "tr" ? Tr : En).TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Tr = new(StringComparer.Ordinal)
    {
        ["skip"] = "Ana icerige atla",
        ["home"] = "Boosters Labs ana sayfa",
        ["menu"] = "Menu",
        ["nav.main"] = "Ana menu",
        ["nav.mobile"] = "Mobil menu",
        ["nav.home"] = "Anasayfa",
        ["nav.products"] = "Urunlerimiz",
        ["nav.games"] = "Oyunlarimiz",
        ["nav.about"] = "Hakkimizda",
        ["nav.contact"] = "Iletisim",
        ["nav.lang"] = "Dil secimi",
        ["lang.switch"] = "English",
        ["cta.contact"] = "Bize ulasin",
        ["cta.explore"] = "Kesfedin",
        ["cta.details"] = "Detaylar",
        ["cta.back.products"] = "Tum urunler",
        ["cta.back.games"] = "Tum oyunlar",
        ["footer.tagline"] = "Kimlik, yetki ve oyun teknolojileri.",
        ["footer.company"] = "Sirket",
        ["footer.products"] = "Urunler",
        ["footer.games"] = "Oyunlar",
        ["footer.contact"] = "Iletisim",
        ["footer.rights"] = "Tum haklari saklidir.",
        ["footer.toTop"] = "Yukari don",
        ["contact.name"] = "Ad Soyad",
        ["contact.email"] = "E-posta",
        ["contact.subject"] = "Konu",
        ["contact.message"] = "Mesajiniz",
        ["contact.send"] = "Gonder",
        ["notfound.title"] = "Sayfa bulunamadi",
        ["notfound.lead"] = "Aradiginiz sayfa tasinmis ya da hic var olmamis olabilir.",
        ["notfound.home"] = "Anasayfaya don",
    };

    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["skip"] = "Skip to main content",
        ["home"] = "Boosters Labs home",
        ["menu"] = "Menu",
        ["nav.main"] = "Main menu",
        ["nav.mobile"] = "Mobile menu",
        ["nav.home"] = "Home",
        ["nav.products"] = "Products",
        ["nav.games"] = "Games",
        ["nav.about"] = "About",
        ["nav.contact"] = "Contact",
        ["nav.lang"] = "Language",
        ["lang.switch"] = "Turkce",
        ["cta.contact"] = "Get in touch",
        ["cta.explore"] = "Explore",
        ["cta.details"] = "Details",
        ["cta.back.products"] = "All products",
        ["cta.back.games"] = "All games",
        ["footer.tagline"] = "Identity, authorization and game technology.",
        ["footer.company"] = "Company",
        ["footer.products"] = "Products",
        ["footer.games"] = "Games",
        ["footer.contact"] = "Contact",
        ["footer.rights"] = "All rights reserved.",
        ["footer.toTop"] = "Back to top",
        ["contact.name"] = "Full name",
        ["contact.email"] = "Email",
        ["contact.subject"] = "Subject",
        ["contact.message"] = "Your message",
        ["contact.send"] = "Send",
        ["notfound.title"] = "Page not found",
        ["notfound.lead"] = "The page you are looking for may have moved or never existed.",
        ["notfound.home"] = "Back to home",
    };
}

/// <summary>Gorunumlere giden ortak model.</summary>
public sealed record SitePageModel(string Lang, SiteCatalog.Page Page)
{
    public string Other => Lang == "tr" ? "en" : "tr";
    public string T(string key) => SiteCatalog.T(Lang, key);
    public string Href(string slug) => slug.Length == 0 ? $"/{Lang}" : $"/{Lang}/{slug}";
}
