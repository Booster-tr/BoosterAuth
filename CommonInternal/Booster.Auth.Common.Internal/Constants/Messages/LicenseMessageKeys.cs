namespace Booster.Auth.Common.Internal.Constants.Messages;

/// <summary>
/// Lisans mesaj anahtarlari. Anahtar ayni zamanda istemciye donen hata kodudur (ProblemDetails "code"); panel bu
/// koda gore kullaniciyi Sistem &gt; Lisans ekranina yonlendirir. Metinler SystemMessage'taki LicenseMessageSeeder'da.
/// </summary>
public static class LicenseMessageKeys
{
    /// <summary>Lisans yok, gecersiz ya da ek sure doldu: panel salt okunur.</summary>
    public const string Restricted = "LICENSE_RESTRICTED";

    /// <summary>Deneme lisansinin ek suresi doldu: son kullanici girisleri durdu.</summary>
    public const string ServiceUnavailable = "LICENSE_SERVICE_UNAVAILABLE";

    // --- Aktivasyon (yapistirilan token reddedildi; KAYDEDILMEZ) ---
    public const string InvalidToken = "LICENSE_INVALID_TOKEN";
    public const string UnknownKey = "LICENSE_UNKNOWN_KEY";
    public const string InstallationMismatch = "LICENSE_INSTALLATION_MISMATCH";
    public const string UnsupportedVersion = "LICENSE_UNSUPPORTED_VERSION";
    public const string Expired = "LICENSE_EXPIRED";
}
