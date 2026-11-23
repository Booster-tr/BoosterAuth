using Microsoft.AspNetCore.Html;

namespace Booster.Licensing.Portal.Site;

/// <summary>Sitede kullanilan satir ici SVG'ler (ikonlar ve mimari semasi). Dis kaynak yok; dil bagimsiz.</summary>
public static class SiteArt
{
    private const string Stroke = "fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";

    public static IHtmlContent Icon(string name) => new HtmlString(name switch
    {
        "key" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"m10.8 12.2 9.2-9.2M17 6l3 3M15 8l2 2\"/></svg>",
        "shield" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><path d=\"M12 3 19 6v6c0 4.4-3 7.8-7 9-4-1.2-7-4.6-7-9V6z\"/><path d=\"m9 12 2 2 4-4\"/></svg>",
        "users" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6\"/><circle cx=\"17\" cy=\"9\" r=\"2.6\"/><path d=\"M16 14.2c2.8.3 5 2.6 5 5.8\"/></svg>",
        "graph" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><circle cx=\"5\" cy=\"6\" r=\"2.2\"/><circle cx=\"19\" cy=\"6\" r=\"2.2\"/><circle cx=\"12\" cy=\"18\" r=\"2.2\"/><path d=\"M7 7.2 10.6 16M17 7.2 13.4 16M7.2 6h9.6\"/></svg>",
        "server" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><rect x=\"3\" y=\"4\" width=\"18\" height=\"7\" rx=\"2\"/><rect x=\"3\" y=\"13\" width=\"18\" height=\"7\" rx=\"2\"/><path d=\"M7 7.5h.01M7 16.5h.01\"/></svg>",
        "panel" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><rect x=\"3\" y=\"4\" width=\"18\" height=\"16\" rx=\"2\"/><path d=\"M3 9h18M9 9v11\"/></svg>",
        "login" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><path d=\"M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4\"/><path d=\"m10 16 4-4-4-4M14 12H4\"/></svg>",
        "globe" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z\"/></svg>",
        "mail" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"m3 7 9 6 9-6\"/></svg>",
        "box" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><path d=\"m12 3 8 4.5v9L12 21l-8-4.5v-9z\"/><path d=\"m4 7.5 8 4.5 8-4.5M12 12v9\"/></svg>",
        "lock" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><rect x=\"5\" y=\"10\" width=\"14\" height=\"10\" rx=\"2\"/><path d=\"M8 10V7a4 4 0 0 1 8 0v3\"/></svg>",
        "bolt" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><path d=\"M13 3 5 14h6l-1 7 8-11h-6z\"/></svg>",
        "code" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><path d=\"m8 8-4 4 4 4M16 8l4 4-4 4M13.5 5l-3 14\"/></svg>",
        "clock" => $"<svg viewBox=\"0 0 24 24\" {Stroke}><circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/></svg>",
        _ => "",
    });

    /// <summary>Ana sayfadaki animasyonlu mimari: uygulamalar -> Booster Auth (IdP + AuthZ) -> kullanici havuzlari.</summary>
    public static IHtmlContent Diagram(string lang)
    {
        var tr = lang != "en";
        string L(string t, string e) => tr ? t : e;
        return new HtmlString($$"""
<svg viewBox="0 0 520 380" role="img" aria-label="{{L("Booster Auth mimarisi", "Booster Auth architecture")}}">
  <g class="node"><rect x="10" y="24" width="140" height="58" rx="12"/><text x="80" y="50" text-anchor="middle">{{L("Web uygulaması", "Web app")}}</text><text class="sub" x="80" y="68" text-anchor="middle">OIDC + PKCE</text></g>
  <g class="node"><rect x="10" y="161" width="140" height="58" rx="12"/><text x="80" y="187" text-anchor="middle">{{L("Mobil uygulama", "Mobile app")}}</text><text class="sub" x="80" y="205" text-anchor="middle">iOS · Android</text></g>
  <g class="node"><rect x="10" y="298" width="140" height="58" rx="12"/><text x="80" y="324" text-anchor="middle">{{L("API / servis", "API / service")}}</text><text class="sub" x="80" y="342" text-anchor="middle">{{L("Servis hesabı", "Service account")}}</text></g>
  <g class="node core"><rect x="200" y="96" width="150" height="188" rx="16"/>
    <text x="275" y="132" text-anchor="middle">Booster Auth</text>
    <text class="sub" x="275" y="160" text-anchor="middle">{{L("Kimlik (IdP)", "Identity (IdP)")}}</text>
    <text class="sub" x="275" y="182" text-anchor="middle">{{L("Yetki (AuthZ)", "Access (AuthZ)")}}</text>
    <text class="sub" x="275" y="204" text-anchor="middle">{{L("Yönetim paneli", "Admin console")}}</text>
    <text class="sub" x="275" y="250" text-anchor="middle">{{L("Sizin sunucunuzda", "On your servers")}}</text></g>
  <g class="node"><rect x="400" y="70" width="110" height="58" rx="12"/><text x="455" y="96" text-anchor="middle">{{L("Havuz A", "Pool A")}}</text><text class="sub" x="455" y="114" text-anchor="middle">{{L("Müşteriler", "Customers")}}</text></g>
  <g class="node"><rect x="400" y="252" width="110" height="58" rx="12"/><text x="455" y="278" text-anchor="middle">{{L("Havuz B", "Pool B")}}</text><text class="sub" x="455" y="296" text-anchor="middle">{{L("Çalışanlar", "Employees")}}</text></g>
  <path id="f1" class="flow" d="M150 53 C180 53 175 140 200 150"/>
  <path id="f2" class="flow" d="M150 190 L200 190"/>
  <path id="f3" class="flow" d="M150 327 C180 327 175 240 200 230"/>
  <path id="f4" class="flow" d="M350 150 C375 140 375 99 400 99"/>
  <path id="f5" class="flow" d="M350 230 C375 240 375 281 400 281"/>
  <circle class="dot" r="4"><animateMotion dur="2.6s" repeatCount="indefinite"><mpath href="#f1"/></animateMotion></circle>
  <circle class="dot" r="4"><animateMotion dur="2.2s" begin=".6s" repeatCount="indefinite"><mpath href="#f2"/></animateMotion></circle>
  <circle class="dot" r="4"><animateMotion dur="2.8s" begin="1.1s" repeatCount="indefinite"><mpath href="#f3"/></animateMotion></circle>
  <circle class="dot" r="4"><animateMotion dur="2.4s" begin=".3s" repeatCount="indefinite"><mpath href="#f4"/></animateMotion></circle>
  <circle class="dot" r="4"><animateMotion dur="2.4s" begin="1.4s" repeatCount="indefinite"><mpath href="#f5"/></animateMotion></circle>
</svg>
""");
    }
}
