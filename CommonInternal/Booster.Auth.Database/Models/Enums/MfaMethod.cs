namespace Booster.Auth.Database.Models.Enums;

[Flags]
public enum MfaMethod
{
    None = 0,
    Totp = 1,
    Sms = 2,
    Email = 4
}
