using Booster.Licensing.Tokens;
using Booster.Licensing.Validation;

namespace Booster.Licensing.Runtime;

/// <summary>
/// Calisan bir servisin lisans durumunu okudugu tek arayuz. Management bunu kendi veritabanindan uretir;
/// IdP ve AuthZ gRPC ile Management'tan alir ve kisa sure onbellekte tutar.
/// </summary>
public interface ILicenseStatusProvider
{
    ValueTask<LicenseStatusSnapshot> GetAsync(CancellationToken ct = default);
}

/// <summary>Servislerin karar icin ihtiyac duydugu ozet. Ham token tasimaz.</summary>
/// <param name="Limits">
/// Uygulanan limitler. Gecerli lisans yoksa (None/Invalid) deneme varsayilanlari (<see cref="TrialDefaultLimits"/>);
/// suresi dolmus lisansta o lisansin limitleri. <c>null</c> = durum bilinemiyor: limit UYGULANMAZ.
/// </param>
public sealed record LicenseStatusSnapshot(
    LicenseState State,
    bool IsTrial,
    bool PanelWritable,
    bool EndUserFlowsAllowed,
    LicenseLimits? Limits)
{
    /// <summary>Lisans yokken uygulanan limitler (deneme lisansinin varsayilanlari).</summary>
    public static readonly LicenseLimits TrialDefaultLimits = new(Users: 100, Pools: 1, Clients: 3);

    /// <summary>
    /// Durum bilinemediginde (Management'a ulasilamiyor) kullanilir: HER SEY ACIK, limit uygulanmaz.
    /// Altyapi arizasi son kullanici girisini ASLA kesmemeli; panel de zaten Management olmadan calismaz.
    /// </summary>
    public static readonly LicenseStatusSnapshot Unknown = new(LicenseState.None, false, true, true, null);

    public static LicenseStatusSnapshot From(LicenseValidationResult r) =>
        new(r.State, r.License?.IsTrial ?? false, r.PanelWritable, r.EndUserFlowsAllowed,
            r.License?.Limits ?? TrialDefaultLimits);
}
