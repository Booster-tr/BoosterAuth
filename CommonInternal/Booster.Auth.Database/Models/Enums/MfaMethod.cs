namespace Booster.Auth.Database.Models.Enums;

[Flags]
public enum MfaMethod
{
    None = 0,
    /// <summary>KULLANILMAZ: TOTP desteklenmiyor. Deger eski veriler icin ayrildi; yeni politikada kabul edilmez.</summary>
    Totp = 1,
    Sms = 2,
    Email = 4
}
