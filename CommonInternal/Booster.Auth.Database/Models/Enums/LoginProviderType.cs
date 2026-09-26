namespace Booster.Auth.Database.Models.Enums;

[Flags]
public enum CredentialProviderType
{
    None = 0,
    Local = 1,
    Ldap = 2,
    Federation = 4
}
