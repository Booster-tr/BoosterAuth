using Microsoft.AspNetCore.Html;

namespace Booster.Licensing.Portal.Site;

/// <summary>
/// Sitede kullanilan satir ici SVG'ler (marka isareti, mimari semasi, lisans zaman cizelgesi).
/// Razor'da &lt;text&gt; ozel etiket oldugu icin SVG'ler burada uretilir. Dis kaynak yok; metinler dile gore.
/// </summary>
public static class SiteArt
{
    /// <summary>Marka isareti (Boosters Auth amblemi). <paramref name="onDark"/>: koyu zemin (footer) icin beyaz amblem.</summary>
    public static IHtmlContent Mark(bool onDark = false) => new HtmlString(onDark
        ? """<img class="mark" src="/img/mark-dark-512.png" alt="" width="26" height="26" />"""
        : """<img class="mark" src="/img/mark-light-512.png" alt="" width="26" height="26" />""");

    /// <summary>Kucuk kilit ikonu (lisans portali baglantisi).</summary>
    public static IHtmlContent Lock() => new HtmlString(
        """<svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true"><rect x="2.5" y="6" width="9" height="6.5" rx="1" fill="none" stroke="currentColor" stroke-width="1.2"/><path d="M4.5 6V4.2a2.5 2.5 0 0 1 5 0V6" fill="none" stroke="currentColor" stroke-width="1.2"/></svg>""");

    /// <summary>
    /// Dagitim mimarisi: kurum sinirinin icinde uygulamalar, LDAP / AD, Booster Auth, ic ag (PostgreSQL, Redis, OpenFGA)
    /// ve lisans dosyasi; sinirin disinda istege bagli dis saglayicilar ve baglanti kurulmayan lisans sunucusu.
    /// </summary>
    public static IHtmlContent Architecture(string lang)
    {
        var tr = lang != "en";
        string L(string t, string e) => tr ? t : e;
        var sub2X = tr ? 250 : 214;
        return new HtmlString($$"""
<svg viewBox="0 0 1120 580" role="img" aria-labelledby="archT archD">
  <title id="archT">{{L("Booster Auth dağıtım mimarisi", "Booster Auth deployment architecture")}}</title>
  <desc id="archD">{{L(
      "Kurumun altyapısı içinde Booster Auth: solda web, mobil ve arka uç uygulamaları OIDC ile, LDAP / Active Directory dizini ise oturum açma için bağlanır; altta dışarıya açılmayan PostgreSQL, Redis ve OpenFGA; sağda yerelde doğrulanan lisans dosyası. Altyapının dışında yalnızca yapılandırılırsa kullanılan dış kimlik sağlayıcıları bulunur; lisans sunucusuna bağlantı kurulmaz.",
      "Booster Auth inside the organisation's infrastructure: web, mobile and back-end applications connect over OIDC on the left, and an LDAP / Active Directory directory is used for sign-in; below, PostgreSQL, Redis and OpenFGA are not exposed; on the right, a license file verified locally. Outside the boundary are external identity providers used only if configured; no connection is made to a license server.")}}</desc>
  <defs>
    <marker id="ah" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 1 L9 5 L0 9" fill="none" stroke="#24564C" stroke-width="1.5"/></marker>
    <marker id="ah2" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0 1 L9 5 L0 9" fill="none" stroke="#9C5E25" stroke-width="1.5"/></marker>
  </defs>
  <g font-family="IBM Plex Sans, system-ui, sans-serif">
    <rect x="20" y="40" width="820" height="520" rx="14" fill="#F4F0E8" stroke="#173F38" stroke-width="1.5"/>
    <text x="44" y="30" font-size="13" font-weight="600" letter-spacing="1.2" fill="#173F38">{{L("KURUMUNUZUN ALTYAPISI", "YOUR INFRASTRUCTURE")}}</text>
    <text x="{{sub2X}}" y="30" font-size="13" fill="#61665F">{{L("· kendi sunucunuz, kendi ağınız", "· your servers, your network")}}</text>

    <text x="56" y="96" font-size="12" font-weight="600" letter-spacing="1" fill="#61665F">{{L("UYGULAMALARINIZ", "YOUR APPLICATIONS")}}</text>
    <rect x="56" y="112" width="210" height="70" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
    <text x="74" y="142" font-size="15" font-weight="500" fill="#1C201E">{{L("Web uygulaması", "Web application")}}</text>
    <text x="74" y="164" font-size="12.5" fill="#61665F" font-family="IBM Plex Mono, monospace">Authorization Code + PKCE</text>
    <rect x="56" y="200" width="210" height="70" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
    <text x="74" y="230" font-size="15" font-weight="500" fill="#1C201E">{{L("Mobil uygulama", "Mobile app")}}</text>
    <text x="74" y="252" font-size="12.5" fill="#61665F" font-family="IBM Plex Mono, monospace">PKCE · refresh token</text>
    <rect x="56" y="288" width="210" height="70" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
    <text x="74" y="318" font-size="15" font-weight="500" fill="#1C201E">{{L("Arka uç servisleri", "Back-end services")}}</text>
    <text x="74" y="340" font-size="12.5" fill="#61665F" font-family="IBM Plex Mono, monospace">service account · M2M</text>

    <text x="56" y="414" font-size="12" font-weight="600" letter-spacing="1" fill="#61665F">{{L("KURUMSAL DİZİN", "CORPORATE DIRECTORY")}}</text>
    <rect x="56" y="430" width="210" height="70" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
    <text x="74" y="460" font-size="15" font-weight="500" fill="#1C201E">LDAP / Active Directory</text>
    <text x="74" y="482" font-size="12.5" fill="#61665F" font-family="IBM Plex Mono, monospace">LDAPS · StartTLS</text>

    <path d="M266 147 C 320 147, 330 200, 378 205" fill="none" stroke="#24564C" stroke-width="1.5" marker-end="url(#ah)"/>
    <path d="M266 235 L 378 235" fill="none" stroke="#24564C" stroke-width="1.5" marker-end="url(#ah)"/>
    <path d="M266 323 C 320 323, 330 270, 378 265" fill="none" stroke="#24564C" stroke-width="1.5" marker-end="url(#ah)"/>
    <rect x="296" y="222" width="52" height="22" rx="3" fill="#F4F0E8"/>
    <text x="322" y="238" font-size="12" text-anchor="middle" font-family="IBM Plex Mono, monospace" fill="#24564C">OIDC</text>
    <path d="M378 300 C 340 330, 330 465, 268 465" fill="none" stroke="#24564C" stroke-width="1.5" marker-end="url(#ah)"/>

    <rect x="380" y="112" width="290" height="230" rx="10" fill="#173F38"/>
    <text x="404" y="146" font-size="12" font-weight="600" letter-spacing="1.2" fill="#D6AE80">BOOSTER AUTH</text>
    <text x="404" y="176" font-size="22" fill="#FFFFFF" font-family="Source Serif 4, Georgia, serif">{{L("Kimlik sağlayıcı", "Identity provider")}}</text>
    <rect x="404" y="196" width="242" height="38" rx="5" fill="#24564C"/>
    <text x="420" y="220" font-size="13.5" fill="#EEF3EF">{{L("OIDC / OAuth 2.0 uç noktaları", "OIDC / OAuth 2.0 endpoints")}}</text>
    <rect x="404" y="242" width="242" height="38" rx="5" fill="#24564C"/>
    <text x="420" y="266" font-size="13.5" fill="#EEF3EF">{{L("Oturum açma, MFA, yetkilendirme", "Sign-in, MFA, authorization")}}</text>
    <rect x="404" y="288" width="242" height="38" rx="5" fill="#24564C"/>
    <text x="420" y="312" font-size="13.5" fill="#EEF3EF">{{L("Yönetim konsolu · TR / EN", "Admin console · TR / EN")}}</text>

    <rect x="360" y="392" width="460" height="140" rx="10" fill="none" stroke="#9C5E25" stroke-dasharray="5 5"/>
    <rect x="376" y="382" width="{{(tr ? 232 : 196)}}" height="20" fill="#F4F0E8"/>
    <text x="384" y="397" font-size="12" font-weight="600" letter-spacing="1" fill="#9C5E25">{{L("İÇ AĞ · DIŞARIYA AÇILMAZ", "INTERNAL · NOT EXPOSED")}}</text>
    <g font-size="14" font-weight="500" fill="#1C201E">
      <rect x="380" y="420" width="132" height="88" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
      <text x="446" y="460" text-anchor="middle">PostgreSQL</text><text x="446" y="482" text-anchor="middle" font-size="11.5" font-weight="400" fill="#61665F">{{L("kalıcı veri", "persistent data")}}</text>
      <rect x="524" y="420" width="132" height="88" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
      <text x="590" y="460" text-anchor="middle">Redis</text><text x="590" y="482" text-anchor="middle" font-size="11.5" font-weight="400" fill="#61665F">{{L("önbellek", "cache")}}</text>
      <rect x="668" y="420" width="132" height="88" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
      <text x="734" y="460" text-anchor="middle">OpenFGA</text><text x="734" y="482" text-anchor="middle" font-size="11.5" font-weight="400" fill="#61665F">{{L("yetkilendirme", "authorization")}}</text>
    </g>
    <path d="M525 342 L 525 382" stroke="#24564C" stroke-width="1.5" marker-end="url(#ah)" marker-start="url(#ah)"/>

    <path d="M712 150 h70 l18 18 v84 h-88 z" fill="#FBF9F4" stroke="#C4BAA8"/>
    <path d="M782 150 v18 h18" fill="none" stroke="#C4BAA8"/>
    <line x1="726" y1="186" x2="784" y2="186" stroke="#D6CEBF" stroke-width="3"/>
    <line x1="726" y1="198" x2="776" y2="198" stroke="#D6CEBF" stroke-width="3"/>
    <line x1="726" y1="210" x2="780" y2="210" stroke="#D6CEBF" stroke-width="3"/>
    <circle cx="770" cy="234" r="9" fill="none" stroke="#9C5E25" stroke-width="1.5"/>
    <path d="M765 234 l3.5 3.5 l6 -7" fill="none" stroke="#9C5E25" stroke-width="1.5"/>
    <text x="756" y="276" text-anchor="middle" font-size="13" font-weight="500" fill="#1C201E">{{L("Lisans dosyası", "License file")}}</text>
    <text x="756" y="294" text-anchor="middle" font-size="12" fill="#61665F">{{L("yerelde doğrulanır", "verified locally")}}</text>
    <path d="M710 200 L 672 200" stroke="#9C5E25" stroke-width="1.5" marker-end="url(#ah2)"/>

    <text x="880" y="96" font-size="12" font-weight="600" letter-spacing="1" fill="#61665F">{{L("DIŞ SAĞLAYICILAR", "EXTERNAL PROVIDERS")}}</text>
    <text x="880" y="114" font-size="12" fill="#61665F">{{L("isteğe bağlı", "optional")}}</text>
    <g font-size="13.5" fill="#1C201E">
      <rect x="880" y="130" width="220" height="196" rx="6" fill="#FBF9F4" stroke="#C4BAA8"/>
      <text x="900" y="160">Google</text><text x="1000" y="160">Microsoft</text>
      <text x="900" y="188">Apple</text><text x="1000" y="188">LinkedIn</text>
      <text x="900" y="216">GitHub</text><text x="1000" y="216">Facebook</text>
      <text x="900" y="244">X</text><text x="1000" y="244">SAML</text>
      <line x1="900" y1="264" x2="1080" y2="264" stroke="#D6CEBF"/>
      <text x="900" y="292" font-family="IBM Plex Mono, monospace" font-size="12.5" fill="#454B47">{{L("genel OIDC sağlayıcı", "generic OIDC provider")}}</text>
    </g>
    <path d="M670 132 C 760 110, 800 120, 878 150" fill="none" stroke="#24564C" stroke-width="1.5" stroke-dasharray="2 4" marker-end="url(#ah)"/>

    <rect x="880" y="420" width="220" height="88" rx="6" fill="none" stroke="#93392B" stroke-dasharray="3 4"/>
    <text x="990" y="456" text-anchor="middle" font-size="14" font-weight="500" fill="#93392B" text-decoration="line-through">{{L("Lisans sunucusu", "License server")}}</text>
    <text x="990" y="480" text-anchor="middle" font-size="12" fill="#61665F">{{L("bağlantı kurulmaz", "never contacted")}}</text>
    <path d="M800 465 L 878 465" stroke="#93392B" stroke-width="1.5" stroke-dasharray="3 4"/>
    <g stroke="#93392B" stroke-width="2"><line x1="832" y1="457" x2="848" y2="473"/><line x1="848" y1="457" x2="832" y2="473"/></g>
  </g>
</svg>
""");
    }

    /// <summary>Ucretli lisansin suresi doldugunda: girisler devam eder, konsol ek sureden sonra salt okunur.</summary>
    public static IHtmlContent Timeline(string lang)
    {
        var tr = lang != "en";
        string L(string t, string e) => tr ? t : e;
        return new HtmlString($$"""
<svg viewBox="0 0 640 262" role="img" aria-labelledby="tlT">
  <title id="tlT">{{L(
      "Ücretli lisansın süresi dolduktan sonra son kullanıcı girişleri kesintisiz devam eder; yönetim konsolu 14 günlük ek süre boyunca tam yetkili, ardından salt okunur olur.",
      "After a paid license expires, end-user sign-ins continue without interruption; the admin console keeps full access for a 14-day grace period, then becomes read-only.")}}</title>
  <g font-family="IBM Plex Sans, system-ui, sans-serif">
    <line x1="20" y1="214" x2="620" y2="214" stroke="#C4BAA8"/>
    <g font-size="15" fill="#61665F">
      <line x1="40" y1="208" x2="40" y2="220" stroke="#C4BAA8"/><text x="40" y="244" text-anchor="middle">{{L("Başlangıç", "Start")}}</text>
      <line x1="330" y1="40" x2="330" y2="220" stroke="#1C201E" stroke-dasharray="3 3"/><text x="330" y="244" text-anchor="middle" fill="#1C201E" font-weight="600">{{L("Bitiş tarihi", "Expiry date")}}</text>
      <line x1="450" y1="40" x2="450" y2="220" stroke="#9C5E25" stroke-dasharray="3 3"/><text x="450" y="244" text-anchor="middle" fill="#9C5E25" font-weight="600">{{L("+14 gün", "+14 days")}}</text>
    </g>
    <text x="20" y="60" font-size="16" font-weight="600" fill="#1C201E">{{L("Son kullanıcı girişleri", "End-user sign-ins")}}</text>
    <rect x="40" y="72" width="580" height="12" rx="6" fill="#2F6B4F"/>
    <text x="620" y="104" text-anchor="end" font-size="15" fill="#2F6B4F">{{L("kesintisiz devam eder →", "continue uninterrupted →")}}</text>
    <text x="20" y="146" font-size="16" font-weight="600" fill="#1C201E">{{L("Yönetim konsolu", "Admin console")}}</text>
    <rect x="40" y="158" width="290" height="12" rx="6" fill="#173F38"/>
    <rect x="324" y="158" width="126" height="12" fill="#9C5E25" opacity=".75"/>
    <rect x="450" y="158" width="170" height="12" fill="#D6CEBF"/>
    <text x="390" y="190" text-anchor="middle" font-size="15" fill="#9C5E25">{{L("ek süre", "grace period")}}</text>
    <text x="535" y="190" text-anchor="middle" font-size="15" fill="#454B47">{{L("salt okunur", "read-only")}}</text>
  </g>
</svg>
""");
    }
}
