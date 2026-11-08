namespace Booster.Auth.Database.Models;


public static class ClientDataCache
{
    // Surum, ClientData'nin bicimi degistiginde artirilir: eski bicimle
    // onbellege yazilmis kayitlar (v1: MFA alani yok) okunmaz, yeniden cekilir.
    // Gecersiz kilma etiketlerle yapildigi icin anahtar degisikligi guvenlidir.
    public static string Key(string clientKey) => $"idp:client:v2:{clientKey}";

    public static string Tag(string clientKey) => $"client:{clientKey}";
    public static string PoolTag(Guid userPoolId) => $"clientpool:{userPoolId}";
}
