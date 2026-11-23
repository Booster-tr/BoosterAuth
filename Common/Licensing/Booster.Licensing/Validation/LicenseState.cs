using Booster.Licensing.Tokens;

namespace Booster.Licensing.Validation;

/// <summary>JSON'da metin olarak yazilir ("Active"); panel ve loglar sayi yerine adi gorur.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LicenseState>))]
public enum LicenseState
{
    /// <summary>Hic lisans girilmemis (yeni kurulum).</summary>
    None = 0,
    Active = 1,
    /// <summary>Bitise <see cref="LicenseStateCalculator.WarningDays"/> gun veya daha az kaldi.</summary>
    Warning = 2,
    /// <summary>Sure doldu, ek sure (graceDays) devam ediyor.</summary>
    Grace = 3,
    /// <summary>Ek sure de doldu.</summary>
    Restricted = 4,
    /// <summary>Bicim, imza, kid, surum veya GUID hatasi.</summary>
    Invalid = 5,
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LicenseInvalidReason>))]
public enum LicenseInvalidReason
{
    Malformed = 1,
    UnsupportedVersion = 2,
    UnknownKid = 3,
    BadSignature = 4,
    /// <summary>Token baska bir kurulumun (installationId) icin uretilmis.</summary>
    InstallationMismatch = 5,
}

/// <summary>Dogrulama sonucu. Panel ve servisler yalnizca bunu okur; ham token'i yorumlamaz.</summary>
public sealed record LicenseValidationResult(
    LicenseState State,
    LicensePayload? License,
    LicenseInvalidReason? InvalidReason,
    DateTimeOffset EvaluatedAt)
{
    public static LicenseValidationResult None(DateTimeOffset at) => new(LicenseState.None, null, null, at);

    public static LicenseValidationResult Invalid(LicenseInvalidReason reason, DateTimeOffset at) =>
        new(LicenseState.Invalid, null, reason, at);

    /// <summary>Bitise kalan tam gun; sure dolduysa negatif. Lisans yoksa null.</summary>
    public int? DaysLeft => License is null ? null : (int)Math.Floor((License.ExpiresAt - EvaluatedAt).TotalDays);

    public DateTimeOffset? GraceEndsAt => License?.ExpiresAt.AddDays(License.GraceDays);

    /// <summary>
    /// Yonetim paneli yazilabilir mi? Restricted, Invalid ve None durumlarinda panel salt okunurdur ve yalnizca lisans
    /// ekrani calisir.
    /// </summary>
    public bool PanelWritable => State is LicenseState.Active or LicenseState.Warning or LicenseState.Grace;

    /// <summary>
    /// Son kullanici login/token/refresh akislari acik mi?
    /// EN KRITIK KURAL: yalnizca DENEME lisansi Restricted olunca kapanir. Ucretli lisans hicbir durumda kesilmez.
    /// Lisans yokken veya Invalid'de de acik kalir. Bu yuzden yapistirilan gecersiz token ASLA kaydedilmez; aksi halde
    /// suresi dolmus bir deneme, cop bir token yapistirilarak yeniden acilabilirdi.
    /// </summary>
    public bool EndUserFlowsAllowed => !(State == LicenseState.Restricted && License is { IsTrial: true });
}
