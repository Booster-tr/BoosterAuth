namespace Booster.Auth.Idp.Managers.CookieManagers;

public interface ISessionCookieManager
{
    void Set(HttpResponse response, string token, DateTimeOffset expiresAt);
    string? Get(HttpRequest request);
    void Clear(HttpResponse response);
}
