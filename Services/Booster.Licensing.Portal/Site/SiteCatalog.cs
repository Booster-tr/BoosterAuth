namespace Booster.Licensing.Portal.Site;

/// <summary>
/// Tanitim sitesinin sayfa kaydi: menu, dokumantasyon yan menusu, sitemap.xml ve hreflang buradan uretilir.
/// Adresler iki dilde AYNIDIR (/tr/docs/concepts, /en/docs/concepts); boylece dil degistirmek yalnizca on eki degistirir.
/// Gorunumler: Views/Site/{tr|en}/{View}.cshtml.
/// </summary>
public static class SiteCatalog
{
    public static readonly string[] Languages = ["tr", "en"];
    public const string DefaultLanguage = "tr";
    public const string ProductName = "Booster Auth";

    public sealed record Page(string Slug, string View, string TitleTr, string TitleEn, string DescTr, string DescEn, string? DocGroup = null)
    {
        public string Title(string lang) => lang == "en" ? TitleEn : TitleTr;
        public string Description(string lang) => lang == "en" ? DescEn : DescTr;
        public string Path(string lang) => Slug.Length == 0 ? $"/{lang}" : $"/{lang}/{Slug}";
    }

    public static readonly Page Home = new("", "Home",
        "Kendi sunucunuzda kimlik ve yetki yönetimi", "Self-hosted identity and access management",
        "Booster Auth: kendi altyapınızda çalışan OpenID Connect kimlik sağlayıcı, kullanıcı havuzları ve ilişki tabanlı yetkilendirme. Docker Compose ile dakikalar içinde kurulur.",
        "Booster Auth: an OpenID Connect identity provider with user pools and relationship-based authorization that runs on your own infrastructure. Up and running in minutes with Docker Compose.");

    public static readonly Page Features = new("features", "Features",
        "Özellikler", "Features",
        "Booster Auth özellikleri: OIDC / OAuth 2.0, parolasız giriş, iki adımlı doğrulama, harici sağlayıcılar, kullanıcı havuzları, OpenFGA tabanlı yetkilendirme ve yönetim paneli.",
        "Booster Auth features: OIDC / OAuth 2.0, passwordless sign-in, two-factor authentication, external identity providers, user pools, OpenFGA-based authorization and an admin console.");

    public static readonly Page Pricing = new("pricing", "Pricing",
        "Paketler ve fiyatlar", "Plans and pricing",
        "Booster Auth paketleri: ücretsiz deneme ve kullanıcı, havuz ve uygulama limitlerine göre yıllık lisanslar. Tüm paketlerde tüm özellikler.",
        "Booster Auth plans: a free trial and annual licenses by user, pool and application limits. Every feature in every plan.");

    public static readonly Page Download = new("download", "Download",
        "İndir", "Download",
        "Booster Auth Docker Compose kurulum paketini indirin: tek makinede tüm servisler, veritabanı ve yönetim paneli.",
        "Download the Booster Auth Docker Compose package: every service, the database and the admin console on a single machine.");

    // ---------------------------------------------------------------- dokumantasyon
    public static readonly Page[] Docs =
    [
        new("docs", "Docs/Index", "Dokümantasyon", "Documentation",
            "Booster Auth dokümantasyonu: kavramlar, kurulum, yapılandırma, uygulama entegrasyonu, yetki kontrolü ve lisans.",
            "Booster Auth documentation: concepts, installation, configuration, application integration, authorization checks and licensing.", "start"),
        new("docs/concepts", "Docs/Concepts", "Temel kavramlar", "Core concepts",
            "Firma (tenant), kullanıcı havuzu, uygulama (client), kullanıcı, rol ve izin nedir; birbirleriyle nasıl ilişkilidir?",
            "What tenants, user pools, applications (clients), users, roles and permissions are and how they relate.", "start"),
        new("docs/installation", "Docs/Installation", "Kurulum", "Installation",
            "Booster Auth'u Docker Compose ile adım adım kurun: paketi indirin, .env dosyasını hazırlayın, docker compose up.",
            "Install Booster Auth step by step with Docker Compose: download the package, prepare the .env file, docker compose up.", "start"),
        new("docs/first-steps", "Docs/FirstSteps", "İlk yapılandırma", "First configuration",
            "Yönetim paneline ilk giriş, firma ve kullanıcı havuzu oluşturma, uygulama ekleme, kullanıcı ekleme ve davet.",
            "First sign-in to the admin console, creating a tenant and a user pool, registering an application, adding and inviting users.", "guide"),
        new("docs/integration", "Docs/Integration", "Uygulama entegrasyonu (OIDC)", "Application integration (OIDC)",
            "Web, SPA, mobil ve sunucu uygulamalarınızı OpenID Connect ile Booster Auth'a bağlayın; örnek kodlar.",
            "Connect your web, SPA, mobile and server applications to Booster Auth with OpenID Connect; code samples.", "guide"),
        new("docs/authorization", "Docs/Authorization", "Yetki kontrolü", "Authorization checks",
            "Roller, izinler ve AuthZ API ile uygulamanızda \"bu kullanıcı bunu yapabilir mi?\" sorusunu yanıtlayın.",
            "Use roles, permissions and the AuthZ API to answer \"can this user do this?\" in your application.", "guide"),
        new("docs/licensing", "Docs/Licensing", "Lisans", "Licensing",
            "Deneme lisansı, satın alma, lisansı panele yükleme, yenileme, aktarım ve süre bitiminde ne olur.",
            "Trial licenses, purchasing, activating a license in the console, renewal, transfer and what happens on expiry.", "operate"),
        new("docs/operations", "Docs/Operations", "İşletim ve sorun giderme", "Operations and troubleshooting",
            "Güncelleme, yedekleme, HTTPS sertifikası, e-posta ayarları ve sık karşılaşılan sorunlar.",
            "Updates, backups, HTTPS certificates, e-mail settings and common problems.", "operate"),
    ];

    public static readonly (string Key, string Tr, string En)[] DocGroups =
    [
        ("start", "Başlarken", "Getting started"),
        ("guide", "Rehberler", "Guides"),
        ("operate", "İşletim", "Operations"),
    ];

    public static IEnumerable<Page> All => new[] { Home, Features, Pricing, Download }.Concat(Docs);

    public static Page? Find(string slug) => All.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.Ordinal));

    /// <summary>Tarayici dili (Accept-Language) Turkce degilse Ingilizce.</summary>
    public static string PreferredLanguage(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return DefaultLanguage;
        foreach (var part in acceptLanguage.Split(','))
        {
            var tag = part.Split(';')[0].Trim().ToLowerInvariant();
            if (tag.StartsWith("tr")) return "tr";
            if (tag.StartsWith("en")) return "en";
        }
        return "en";
    }

    /// <summary>Iki dilli kisa arayuz metinleri (menu, butonlar, footer).</summary>
    public static string T(string lang, string key) => (lang == "en" ? En : Tr).TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Tr = new()
    {
        ["nav.product"] = "Ürün", ["nav.features"] = "Özellikler", ["nav.docs"] = "Dokümantasyon",
        ["nav.pricing"] = "Paketler", ["nav.download"] = "İndir", ["nav.portal"] = "Lisans portalı",
        ["cta.download"] = "Ücretsiz indir", ["cta.trial"] = "Ücretsiz deneme al", ["cta.pricing"] = "Paketleri incele",
        ["cta.docs"] = "Dokümantasyonu oku", ["cta.buy"] = "Lisans al", ["cta.start"] = "Hemen başla",
        ["lang.switch"] = "English", ["menu"] = "Menü",
        ["footer.tagline"] = "Kendi altyapınızda çalışan kimlik ve yetki yönetimi.",
        ["footer.product"] = "Ürün", ["footer.resources"] = "Kaynaklar", ["footer.account"] = "Hesap",
        ["footer.signin"] = "Giriş yap", ["footer.orders"] = "Siparişlerim", ["footer.installations"] = "Kurulumlarım",
        ["footer.rights"] = "Tüm hakları saklıdır.",
        ["docs.on_this_page"] = "Bu sayfada", ["docs.next"] = "Sonraki", ["docs.prev"] = "Önceki",
        ["copy"] = "Kopyala", ["copied"] = "Kopyalandı",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["nav.product"] = "Product", ["nav.features"] = "Features", ["nav.docs"] = "Docs",
        ["nav.pricing"] = "Pricing", ["nav.download"] = "Download", ["nav.portal"] = "License portal",
        ["cta.download"] = "Download free", ["cta.trial"] = "Get a free trial", ["cta.pricing"] = "See plans",
        ["cta.docs"] = "Read the docs", ["cta.buy"] = "Get a license", ["cta.start"] = "Get started",
        ["lang.switch"] = "Türkçe", ["menu"] = "Menu",
        ["footer.tagline"] = "Identity and access management that runs on your own infrastructure.",
        ["footer.product"] = "Product", ["footer.resources"] = "Resources", ["footer.account"] = "Account",
        ["footer.signin"] = "Sign in", ["footer.orders"] = "My orders", ["footer.installations"] = "My installations",
        ["footer.rights"] = "All rights reserved.",
        ["docs.on_this_page"] = "On this page", ["docs.next"] = "Next", ["docs.prev"] = "Previous",
        ["copy"] = "Copy", ["copied"] = "Copied",
    };
}
