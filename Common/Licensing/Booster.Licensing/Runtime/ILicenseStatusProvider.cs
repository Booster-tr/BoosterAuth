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
public sealed record LicenseStatusSnapshot(LicenseState State, bool IsTrial, bool PanelWritable, bool EndUserFlowsAllowed)
{
    /// <summary>
    /// Durum bilinemediginde (Management'a ulasilamiyor) kullanilir: HER SEY ACIK.
    /// Altyapi arizasi son kullanici girisini ASLA kesmemeli; panel de zaten Management olmadan calismaz.
    /// </summary>
    public static readonly LicenseStatusSnapshot Unknown = new(LicenseState.None, false, true, true);

    public static LicenseStatusSnapshot From(LicenseValidationResult r) =>
        new(r.State, r.License?.IsTrial ?? false, r.PanelWritable, r.EndUserFlowsAllowed);
}
