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

    // --- Limitler (yeni kayit olusturma reddedildi; mevcut kayitlar ETKILENMEZ) ---
    public const string LimitUsers = "LICENSE_LIMIT_USERS";
    public const string LimitPools = "LICENSE_LIMIT_POOLS";
    public const string LimitClients = "LICENSE_LIMIT_CLIENTS";

    /// <summary>Self-servis kayit / federasyon ilk girisi limit nedeniyle kapali. Son kullaniciya teknik ayrinti gosterilmez.</summary>
    public const string RegistrationClosed = "LICENSE_REGISTRATION_CLOSED";

    /// <summary>Lisans durumu alinamadi (Management'a ulasilamiyor): yeni kayit olusturulmaz; mevcut girisler etkilenmez.</summary>
    public const string StatusUnavailable = "LICENSE_STATUS_UNAVAILABLE";

    /// <summary>Kullanim lisans limitini asti: panel salt okunur, yalnizca silme serbest.</summary>
    public const string OverLimit = "LICENSE_OVER_LIMIT";

    /// <summary>Lisansin saat kaydi silinmis ya da degistirilmis: lisans suresi dolmus sayilir.</summary>
    public const string ClockTampered = "LICENSE_CLOCK_TAMPERED";
}
