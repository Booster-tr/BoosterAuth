namespace Booster.Auth.Idp.Constants;


public static class OidcScopes
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";
    public const string OfflineAccess = "offline_access";

    public static readonly string[] All = [OpenId, Profile, Email, OfflineAccess];

    private static readonly HashSet<string> Lookup = new(All, StringComparer.Ordinal);

    public static bool IsStandard(string scope) => Lookup.Contains(scope);
}
