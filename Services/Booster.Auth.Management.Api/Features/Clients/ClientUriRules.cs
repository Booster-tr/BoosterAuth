namespace Booster.Auth.Management.Api.Features.Clients;

/// <summary>
/// Uygulama adreslerinin bicim kurallari (olusturma ve guncelleme ayni kurali kullanir).
/// Yalnizca <c>Uri.TryCreate(Absolute)</c> yetmiyordu: <c>javascript:</c>, <c>data:</c>, <c>file:</c> gibi semalar ve
/// <c>#</c> iceren adresler kaydediliyordu (RFC 6749 3.1.2: yonlendirme adresi fragment icermez).
/// Mobil uygulamalarin ozel semalari (<c>com.firma.app:/cb</c>, RFC 8252) serbesttir; havuzun yonlendirme politikasi
/// bunlari ayrica kisitlayabilir.
/// </summary>
public static class ClientUriRules
{
    private static readonly HashSet<string> ForbiddenSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript", "vbscript", "data", "file", "blob", "about", "filesystem", "view-source"
    };

    public static bool IsValidRedirectUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('#') || value.Any(char.IsWhiteSpace))
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || ForbiddenSchemes.Contains(uri.Scheme))
            return false;

        return !IsWeb(uri) || !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>Audience (izin verilen API) tam bir http(s) adresi olmalidir; token'daki <c>aud</c> budur.</summary>
    public static bool IsValidAudience(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && !value.Contains('#')
           && !value.Any(char.IsWhiteSpace)
           && Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && IsWeb(uri)
           && !string.IsNullOrEmpty(uri.Host)
           && string.IsNullOrEmpty(uri.Query);

    private static bool IsWeb(Uri uri) => uri.Scheme is "http" or "https";
}
