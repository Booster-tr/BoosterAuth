namespace Booster.Licensing.Portal.Site;

/// <summary>
/// Tanitim sitesinin sayfa kaydi: menu, dokumantasyon yan menusu, sitemap.xml ve hreflang buradan uretilir.
/// Adresler iki dilde AYNIDIR (/tr/docs/concepts, /en/docs/concepts); boylece dil degistirmek yalnizca on eki degistirir.
/// Gorunumler: Views/Site/{tr|en}/{View}.cshtml.
/// Title: menu / yan menu / sayfa ici kisa ad. SeoTitle: &lt;title&gt; ve og:title (bos ise Title).
/// </summary>
public static class SiteCatalog
{
    public static readonly string[] Languages = ["tr", "en"];
    public const string DefaultLanguage = "tr";
    public const string ProductName = "Booster Auth";

    // Destek iletisim bilgileri: site, portal ve dokumanlar buradan okur (tek yerde).
    public const string SupportPhone = "0540 526 26 26";
    public const string SupportPhoneHref = "tel:+905405262626";
    public const string SupportEmail = "helpdesk@boosterauth.com";

    public sealed record Page(string Slug, string View, string TitleTr, string TitleEn, string DescTr, string DescEn,
        string? DocGroup = null, string? SeoTr = null, string? SeoEn = null)
    {
        public string Title(string lang) => lang == "en" ? TitleEn : TitleTr;
        public string SeoTitle(string lang) => (lang == "en" ? SeoEn : SeoTr) ?? Title(lang);
        public string Description(string lang) => lang == "en" ? DescEn : DescTr;
        public string Path(string lang) => Slug.Length == 0 ? $"/{lang}" : $"/{lang}/{Slug}";
    }

    public static readonly Page Home = new("", "Home",
        "Ana sayfa", "Home",
        "Booster Auth, kendi sunucunuzda çalışan kimlik ve erişim yönetimidir: OpenID Connect, LDAP / Active Directory ile giriş, MFA ve yetkilendirme. Tek komutla kurulur.",
        "Booster Auth is self-hosted identity and access management: OpenID Connect, LDAP / Active Directory sign-in, MFA and authorization. Installed with a single command.",
        SeoTr: "Kendi sunucunuzda kimlik ve erişim yönetimi", SeoEn: "Self-hosted identity and access management");

    public static readonly Page Features = new("features", "Features",
        "Özellikler", "Features",
        "OIDC / OAuth 2.0, LDAP / Active Directory ile giriş, parolasız giriş, e-posta / SMS ile iki adımlı doğrulama, kullanıcı havuzları, OpenFGA yetkilendirme ve sertleştirilmiş kurulum.",
        "OIDC / OAuth 2.0, LDAP / Active Directory sign-in, passwordless codes, two-factor via e-mail / SMS, isolated user pools, OpenFGA authorization and a hardened deployment.",
        SeoTr: "Özellikler: OIDC, LDAP / Active Directory, MFA ve yetkilendirme", SeoEn: "Features: OIDC, LDAP / Active Directory, MFA and authorization");

    public static readonly Page Pricing = new("pricing", "Pricing",
        "Paketler", "Pricing",
        "Booster Auth paketleri: 7 günlük ücretsiz deneme ve kurulum başına yıllık lisanslar. Tüm özellikler her pakette; kullanıcı başına aylık ücret yok.",
        "Booster Auth plans: a 7-day free trial and annual per-installation licenses. Every feature in every plan, and no per-user monthly fee.",
        SeoTr: "Paketler ve fiyatlar: kurulum başına yıllık lisans", SeoEn: "Plans and pricing: annual per-installation licenses");

    public static readonly Page Download = new("download", "Download",
        "İndir", "Download",
        "booster-auth.zip kurulum paketini indirin. Kurulum betiği tüm parola ve anahtarları sizin sunucunuzda üretir; docker compose up -d ile sistem ayağa kalkar.",
        "Download the booster-auth.zip package. The setup script generates every password and key on your server, and docker compose up -d starts the whole system.",
        SeoTr: "İndir: Docker Compose kurulum paketi", SeoEn: "Download: the Docker Compose install package");

    public static readonly Page Help = new("help", "Help",
        "Yardım", "Help",
        "Booster Auth destek: telefon ve e-posta ile bize ulaşın. Kurulum, lisans ve entegrasyon sorularınız için talep oluştururken gerekenler.",
        "Booster Auth support: reach us by phone and e-mail. What to include when you contact us about installation, licensing or integration.",
        SeoTr: "Yardım ve destek", SeoEn: "Help and support");

    // ---------------------------------------------------------------- dokumantasyon
    public static readonly Page[] Docs =
    [
        new("docs", "Docs/Index", "Genel bakış", "Overview",
            "Booster Auth dokümantasyonu: kavramlar, kurulum, ilk yapılandırma, OIDC entegrasyonu, yetki kontrolü, lisans ve işletim.",
            "Booster Auth documentation: concepts, installation, first configuration, OIDC integration, authorization checks, licensing and operations.", "start",
            SeoTr: "Dokümantasyon", SeoEn: "Documentation"),
        new("docs/concepts", "Docs/Concepts", "Temel kavramlar", "Core concepts",
            "Firma (tenant), kullanıcı havuzu, uygulama (client), kullanıcı, servis hesabı, rol, kapsam ve yetki şeması nedir; birbirleriyle nasıl ilişkilidir?",
            "What tenants, user pools, applications (clients), users, service accounts, roles, scopes and the authorization schema are, and how they relate.", "start",
            SeoTr: "Temel kavramlar: firma, havuz, uygulama, rol", SeoEn: "Core concepts: tenants, pools, applications, roles"),
        new("docs/installation", "Docs/Installation", "Kurulum", "Installation",
            "Booster Auth'u Docker Compose ile kurun: paketi indirin, kurulum betiğini çalıştırın, docker compose up -d. Gereksinimler, .env değişkenleri ve HTTPS.",
            "Install Booster Auth with Docker Compose: download the package, run the setup script, docker compose up -d. Requirements, .env variables and HTTPS.", "start",
            SeoTr: "Kurulum: Docker Compose ile adım adım", SeoEn: "Installation: step by step with Docker Compose"),
        new("docs/first-steps", "Docs/FirstSteps", "İlk yapılandırma", "First configuration",
            "Yönetim konsoluna ilk giriş, firma ve kullanıcı havuzu oluşturma, uygulama kaydetme, kullanıcı ekleme, davet ve CSV ile içe aktarma.",
            "First sign-in to the admin console, creating a tenant and a user pool, registering an application, adding, inviting and importing users.", "guide"),
        new("docs/integration", "Docs/Integration", "Uygulama entegrasyonu (OIDC)", "Application integration (OIDC)",
            "Web, SPA, mobil ve sunucu uygulamalarınızı OpenID Connect ile Booster Auth'a bağlayın: issuer, uç noktalar, PKCE, token içerikleri ve örnek kodlar.",
            "Connect web, SPA, mobile and server applications to Booster Auth with OpenID Connect: issuer, endpoints, PKCE, token contents and code samples.", "guide"),
        new("docs/authorization", "Docs/Authorization", "Yetki kontrolü", "Authorization checks",
            "Roller, kapsamlar ve OpenFGA tabanlı yetki şemasıyla \"bu kullanıcı bunu yapabilir mi?\" sorusunu AuthZ API ile yanıtlayın.",
            "Use roles, scopes and the OpenFGA-based authorization schema to answer \"can this user do this?\" with the AuthZ API.", "guide"),
        new("docs/pool-apis", "Docs/PoolApis", "Havuz API'leri", "Pool APIs",
            "Uygulamanızın backend'i, havuzun servis hesabıyla kendi kullanıcılarına rol ve kapsam versin, yetki şemasına ilişki yazsın ve yetki sorgulasın.",
            "Let your application's backend grant roles and scopes to its users, write relations to the authorization schema and check permissions with a pool service account.", "guide",
            SeoTr: "Havuz API'leri: rol, kapsam ve ilişki yönetimi", SeoEn: "Pool APIs: managing roles, scopes and relations"),
        new("docs/ai", "Docs/Ai", "AI ile entegrasyon", "Integrating with AI",
            "Booster Auth entegrasyonunu bir AI kodlama asistanıyla yapın: llms.txt ve Claude için hazır skill dosyası.",
            "Integrate Booster Auth with an AI coding assistant: llms.txt and a ready-made skill file for Claude.", "guide",
            SeoTr: "AI kodlama asistanlarıyla entegrasyon (llms.txt, skill)", SeoEn: "Integrating with AI coding assistants (llms.txt, skill)"),
        new("docs/account", "Docs/Account", "Hesabım (son kullanıcı)", "My account (end users)",
            "Kullanıcılarınız profilini, e-postasını, telefonunu, parolasını ve oturumlarını kendisi yönetsin; isterse hesabını silsin. Uygulamanıza tek bir bağlantı yeter.",
            "Let users manage their own profile, email, phone, password and sessions, and delete their account if allowed. A single link in your app is enough.", "guide",
            SeoTr: "Son kullanıcı hesap yönetimi (Hesabım)", SeoEn: "End-user self-service (My account)"),
        new("docs/ldap", "Docs/Ldap", "LDAP / Active Directory", "LDAP / Active Directory",
            "Kullanıcılarınız kurum dizini parolasıyla girsin: LDAP / Active Directory bağlantısı, LDAPS, alan eşleme, ilk girişte hesap ve güvenlik kuralları.",
            "Let users sign in with their directory password: connecting LDAP / Active Directory, LDAPS, attribute mapping, first sign-in accounts and security rules.", "guide",
            SeoTr: "LDAP ve Active Directory ile giriş", SeoEn: "Sign in with LDAP and Active Directory"),
        new("docs/licensing", "Docs/Licensing", "Lisans", "Licensing",
            "Çevrimdışı lisans: kurulum kimliği, deneme, satın alma, lisansı konsolda etkinleştirme, lisans durumları, yenileme ve aktarım.",
            "Offline licensing: installation ID, trial, purchasing, activating a license in the console, license states, renewal and transfer.", "operate"),
        new("docs/operations", "Docs/Operations", "İşletim ve sorun giderme", "Operations and troubleshooting",
            "Booster Auth'u işletmek: günlük komutlar, güncelleme, yedekleme, KVKK'ya uygun log ve denetim kayıtları, e-posta ayarları ve sık karşılaşılan sorunların çözümü.",
            "Running Booster Auth: everyday commands, updates, backups, privacy-minded logs and audit records, e-mail settings and fixes for common problems.", "operate"),
    ];

    public static readonly (string Key, string Tr, string En)[] DocGroups =
    [
        ("start", "Başlarken", "Getting started"),
        ("guide", "Rehberler", "Guides"),
        ("operate", "İşletim", "Operations"),
    ];

    // ---------------------------------------------------------------- yasal metinler (/{dil}/legal/...)
    // Sirket bilgileri LegalInfo'dan okunur. Gorunumler: Views/Site/{tr|en}/Legal/*.cshtml (_LegalLayout).
    public static readonly Page Imprint = new("legal/imprint", "Legal/Imprint", "Künye", "Company information",
        "Booster Auth'u sunan şirketin unvanı, MERSİS ve sicil bilgileri, adresi, KEP adresi ve iletişim bilgileri.",
        "Legal name, registration details, address, registered e-mail (KEP) and contact details of the company behind Booster Auth.",
        SeoTr: "Künye ve iletişim bilgileri", SeoEn: "Company information and contact details");

    public static readonly Page TermsOfUse = new("legal/terms", "Legal/Terms", "Site Kullanım Koşulları", "Website terms of use",
        "Booster Auth web sitesinin, dokümantasyonun ve lisans portalının kullanım koşulları.",
        "Terms of use for the Booster Auth website, documentation and license portal.");

    public static readonly Page LicenseAgreement = new("legal/license-agreement", "Legal/LicenseAgreement", "Yazılım Lisans Sözleşmesi", "Software License Agreement",
        "Booster Auth yazılımının kurulum başına, yıllık ve çevrimdışı lisans koşulları; deneme lisansı, sorumluluk ve fesih.",
        "Terms of the per-installation, annual, offline Booster Auth license: trial license, liability and termination.");

    public static readonly Page SalesTerms = new("legal/sales-terms", "Legal/SalesTerms", "Satış ve Sipariş Koşulları", "Sales and Order Terms",
        "Sipariş adımları, havale / EFT ile ödeme, lisans anahtarının teslimi, fatura, yenileme, iptal ve iade.",
        "Order steps, payment by bank transfer, delivery of the license key, invoicing, renewal, cancellation and refunds.");

    public static readonly Page Privacy = new("legal/privacy", "Legal/Privacy", "Aydınlatma Metni ve Gizlilik", "Privacy Notice",
        "KVKK aydınlatma metni: web sitesi ve lisans portalında hangi kişisel verileri, hangi amaç ve hukuki sebeple işlediğimiz, kimlere aktardığımız ve haklarınız.",
        "Privacy notice under the Turkish data protection law (KVKK): what personal data we process on the website and license portal, why, who receives it and your rights.",
        SeoTr: "KVKK Aydınlatma Metni ve Gizlilik", SeoEn: "Privacy Notice (KVKK)");

    public static readonly Page CookiePolicy = new("legal/cookies", "Legal/Cookies", "Çerez Politikası", "Cookie Policy",
        "Web sitesi ve lisans portalında kullanılan çerezler: adları, amaçları ve süreleri. Yalnızca zorunlu çerezler kullanılır.",
        "Cookies used on the website and license portal: names, purposes and lifetimes. Only strictly necessary cookies are used.");

    public static readonly Page SupportPolicy = new("legal/support", "Legal/Support", "Destek Politikası ve Veri İşleme Eki", "Support Policy and Data Processing Addendum",
        "Destek kanalları ve hedef yanıt süreleri; destek sırasında paylaşılan log ve ekran görüntülerinin işlenmesine ilişkin veri işleme eki.",
        "Support channels and target response times, and the data processing addendum for logs and screenshots shared with support.");

    public static readonly Page ThirdParty = new("legal/third-party", "Legal/ThirdParty", "Üçüncü Taraf Bildirimleri", "Third-party notices",
        "Booster Auth kurulum paketinin kullandığı açık kaynak bileşenler ve lisansları.",
        "Open-source components used by the Booster Auth installation package and their licenses.",
        SeoTr: "Üçüncü taraf ve açık kaynak bildirimleri", SeoEn: "Third-party and open-source notices");

    public static readonly Page[] Legal = [Imprint, TermsOfUse, LicenseAgreement, SalesTerms, Privacy, CookiePolicy, SupportPolicy, ThirdParty];

    public static IEnumerable<Page> All => new[] { Home, Features, Pricing, Download, Help }.Concat(Docs).Concat(Legal);

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
        ["nav.main"] = "Ana menü", ["nav.mobile"] = "Mobil menü", ["nav.lang"] = "Dil", ["home"] = "Booster Auth ana sayfa",
        ["cta.download"] = "İndir ve dene", ["cta.trial"] = "7 gün ücretsiz deneyin", ["cta.pricing"] = "Paketleri inceleyin",
        ["cta.docs"] = "Dokümantasyon", ["cta.buy"] = "Lisans al", ["cta.start"] = "Hemen başla",
        ["lang.switch"] = "English", ["menu"] = "Menü", ["skip"] = "İçeriğe geç",
        ["nav.help"] = "Yardım", ["footer.support"] = "Destek", ["footer.helpcenter"] = "Yardım merkezi",
        ["footer.tagline"] = "Kendi sunucunuzda çalışan kimlik ve erişim yönetimi.",
        ["footer.product"] = "Ürün", ["footer.resources"] = "Dokümantasyon", ["footer.account"] = "Hesap",
        ["footer.signin"] = "Lisans portalına giriş", ["footer.orders"] = "Siparişlerim", ["footer.installations"] = "Kurulumlarım",
        ["footer.rights"] = "Tüm hakları saklıdır.", ["footer.note"] = "Çevrimdışı lisans · lisans sunucusuna bağlanmaz",
        ["docs.on_this_page"] = "Bu sayfada", ["docs.next"] = "Sonraki", ["docs.prev"] = "Önceki", ["docs.nav"] = "Dokümantasyon menüsü",
        ["docs.pager"] = "Önceki ve sonraki sayfa", ["docs.label"] = "Dokümantasyon",
        ["copy"] = "Kopyala", ["copied"] = "Kopyalandı",
        ["footer.legal"] = "Yasal", ["legal.label"] = "Yasal metinler", ["legal.nav"] = "Yasal metinler menüsü",
        ["legal.updated"] = "Son güncelleme", ["legal.version"] = "Sürüm",
        ["legal.draft"] = "Bu metin taslaktır; şirket bilgileri tamamlandıktan ve hukuki incelemeden sonra yürürlüğe girer.",
        ["legal.save"] = "Bu metni tarayıcınızın yazdırma menüsüyle PDF olarak kaydedip saklayabilirsiniz.",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["nav.product"] = "Product", ["nav.features"] = "Features", ["nav.docs"] = "Docs",
        ["nav.pricing"] = "Pricing", ["nav.download"] = "Download", ["nav.portal"] = "License portal",
        ["nav.main"] = "Main menu", ["nav.mobile"] = "Mobile menu", ["nav.lang"] = "Language", ["home"] = "Booster Auth home",
        ["cta.download"] = "Download and try", ["cta.trial"] = "Try it free for 7 days", ["cta.pricing"] = "See plans",
        ["cta.docs"] = "Documentation", ["cta.buy"] = "Get a license", ["cta.start"] = "Get started",
        ["lang.switch"] = "Türkçe", ["menu"] = "Menu", ["skip"] = "Skip to content",
        ["nav.help"] = "Help", ["footer.support"] = "Support", ["footer.helpcenter"] = "Help center",
        ["footer.tagline"] = "Identity and access management that runs on your own servers.",
        ["footer.product"] = "Product", ["footer.resources"] = "Documentation", ["footer.account"] = "Account",
        ["footer.signin"] = "Sign in to the license portal", ["footer.orders"] = "My orders", ["footer.installations"] = "My installations",
        ["footer.rights"] = "All rights reserved.", ["footer.note"] = "Offline licensing · never contacts a license server",
        ["docs.on_this_page"] = "On this page", ["docs.next"] = "Next", ["docs.prev"] = "Previous", ["docs.nav"] = "Documentation menu",
        ["docs.pager"] = "Previous and next page", ["docs.label"] = "Documentation",
        ["copy"] = "Copy", ["copied"] = "Copied",
        ["footer.legal"] = "Legal", ["legal.label"] = "Legal", ["legal.nav"] = "Legal documents menu",
        ["legal.updated"] = "Last updated", ["legal.version"] = "Version",
        ["legal.draft"] = "This text is a draft. It takes effect once the company details are completed and it has passed legal review.",
        ["legal.save"] = "You can save this text as a PDF using your browser's print menu.",
    };
}
