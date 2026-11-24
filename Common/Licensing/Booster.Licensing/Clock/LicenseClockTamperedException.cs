namespace Booster.Licensing.Clock;

/// <summary>
/// Saklanan en ileri zaman silinmis, cozulemiyor ya da baska bir kuruluma ait. Saat geri alma korumasi atlatilmaya
/// calisilmis sayilir; cagiran lisansi suresi dolmus kabul eder.
/// </summary>
public sealed class LicenseClockTamperedException(string reason) : Exception(reason);
