namespace Booster.Observability;

/// <summary>
/// "Observability" bolumu. OtlpEndpoint bos ise hicbir sey disariya gonderilmez; loglar yalnizca konsola (stdout)
/// yazilir (docker compose logs / kubectl logs). Doluysa log, trace ve metrik OTLP/HTTP ile gonderilir.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>OTLP taban adresi, or. OpenObserve: <c>http://openobserve:5080/api/default</c>. Bos = kapali.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>OTLP basliklari "ad=deger,ad2=deger2"; or. <c>Authorization=Basic ...</c>.</summary>
    public string? OtlpHeaders { get; set; }

    /// <summary>
    /// OpenObserve kullanici adi / parolasi. Verilirse "Authorization: Basic" basligi bunlardan uretilir; boylece servisler
    /// OpenObserve ile AYNI degerleri kullanir (.env'de tek yer: OPENOBSERVE_ADMIN_EMAIL / OPENOBSERVE_ADMIN_PASSWORD).
    /// OtlpHeaders'ta Authorization varsa o kullanilir (baska bir OTLP alicisi).
    /// </summary>
    public string? OtlpUser { get; set; }

    public string? OtlpPassword { get; set; }

    /// <summary>Isletim loglarinin akisi (kisa saklama).</summary>
    public string LogStream { get; set; } = "booster";

    /// <summary>Denetim kayitlarinin akisi (uzun saklama; bkz. AuditLog).</summary>
    public string AuditStream { get; set; } = "audit";
}
