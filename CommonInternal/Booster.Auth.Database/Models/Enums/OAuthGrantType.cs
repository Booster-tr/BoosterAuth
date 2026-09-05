namespace Booster.Auth.Database.Models.Enums;

[Flags]
public enum OAuthGrantType
{
    None = 0,
    AuthorizationCode = 1,
    ClientCredentials = 2,
    RefreshToken = 4,
    TokenExchange = 8,
    ContextSwitch = 16
}

