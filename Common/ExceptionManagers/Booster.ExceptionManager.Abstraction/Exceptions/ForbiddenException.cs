namespace Booster.ExceptionManager.Abstraction.Exceptions;

/// <summary>
/// YETKİ reddi — istek geçerli, çağıranın hakkı yok. HTTP 403 döner.
/// <para>
/// <see cref="BusinessException"/>'dan türer: hâlâ bir iş kuralı ihlalidir ve mesaj anahtarı aynı
/// şekilde çevrilir; ayrıştıran tek şey durum kodudur. Mevcut <c>catch (BusinessException)</c>
/// yakalayıcıları bunu da yakalamaya devam eder.
/// </para>
/// <para>
/// Hatalı istekle KARIŞTIRMA: eksik alan, bozuk id, tanımsız tip → <see cref="BusinessException"/>
/// (400). "Bunu yapmaya hakkın yok" → bu (403).
/// </para>
/// </summary>
public class ForbiddenException(string messageKey) : BusinessException(messageKey);
