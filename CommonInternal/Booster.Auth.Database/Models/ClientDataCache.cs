namespace Booster.Auth.Database.Models;


public static class ClientDataCache
{
    public static string Key(string clientKey) => $"idp:client:{clientKey}";

    public static string Tag(string clientKey) => $"client:{clientKey}";
    public static string PoolTag(Guid userPoolId) => $"clientpool:{userPoolId}";
}
