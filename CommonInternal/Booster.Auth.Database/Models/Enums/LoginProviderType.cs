namespace Booster.Auth.Database.Models.Enums;

[Flags]
public enum LoginProviderType
{
    None = 0,

    Local = 1,

    Ldap = 2,

    Federation = 4,

    EmailCode = 8,

    PhoneCode = 16
}
