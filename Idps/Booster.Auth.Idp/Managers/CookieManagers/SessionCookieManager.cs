namespace Booster.Auth.Idp.Managers.CookieManagers;

public sealed class SessionCookieManager : ISessionCookieManager
{
    private const string CookieName = "booster_idp_session";

    public void Set(HttpResponse response, string token, DateTimeOffset expiresAt)
        => response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,             
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt,
            Path = "/",
            IsEssential = true
        });

    public string? Get(HttpRequest request)
        => request.Cookies.TryGetValue(CookieName, out var token) ? token : null;

    public void Clear(HttpResponse response)
        => response.Cookies.Delete(CookieName);
}
