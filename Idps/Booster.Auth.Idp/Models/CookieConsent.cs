namespace Booster.Auth.Idp.Models;

/// <summary>
/// Kullanicinin cerez tercihi. Zorunlu cerezler tercih DEGILDIR (her zaman
/// aciktir), bu yuzden modelde alanlari yoktur.
/// <para>
/// Bicim: <c>v1.f1.a0</c> -- surum, islevsel, analitik. Yalnizca harf, rakam ve
/// nokta kullanilir: ASP.NET cerez degerini URL-kodlar, JSON ya da ayrac
/// karakterleri kodlanip auth.js'in yazdigi degerle uyusmazdi. auth.js ayni
/// bicimi yazar (cookieConsent modulu); biri degisirse digeri de degismelidir.
/// </para>
/// <para>
/// Surum, cerez politikasi esasli bicimde degistiginde artirilir: eski surumlu
/// cerez gecersiz sayilir ve bar yeniden gorunur -- eski rizaya dayanarak yeni
/// bir amac icin cerez kullanilmaz.
/// </para>
/// </summary>
public sealed record CookieConsent(bool Functional, bool Analytics)
{
    public const string CookieName = "booster_cookie_consent";

    public const int Version = 1;

    public static readonly CookieConsent AcceptAll = new(true, true);

    public static readonly CookieConsent RejectAll = new(false, false);

    public string Serialize() => $"v{Version}.f{(Functional ? 1 : 0)}.a{(Analytics ? 1 : 0)}";

    /// <summary>Gecerli bir karar yoksa null: bar gosterilir.</summary>
    public static CookieConsent? Read(HttpRequest request)
        => Parse(request.Cookies[CookieName]);

    public static CookieConsent? Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var parts = value.Split('.');
        if (parts.Length != 3 || parts[0] != $"v{Version}")
            return null;

        var functional = Flag(parts[1], 'f');
        var analytics = Flag(parts[2], 'a');

        return functional is null || analytics is null
            ? null
            : new CookieConsent(functional.Value, analytics.Value);
    }

    public void Write(HttpResponse response)
        => response.Cookies.Append(CookieName, Serialize(), new CookieOptions
        {
            // HttpOnly DEGIL: istemci tarafi betikler (ileride eklenecek analitik
            // yukleyici gibi) yuklenmeden once bu karari okuyabilmelidir. Deger
            // gizli bir bilgi tasimaz.
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Path = "/",
            // Kararin kendisini saklayan cerez zorunlu cerezdir; yoksa "reddet"
            // secimi hatirlanamaz ve bar her sayfada tekrar sorardi.
            IsEssential = true
        });

    private static bool? Flag(string part, char prefix)
        => part.Length == 2 && part[0] == prefix
            ? part[1] switch { '1' => true, '0' => false, _ => null }
            : null;
}
