namespace Booster.Caching.Abstraction;

public interface ICacheOptions
{
    /// <summary>Varsayılan girdi ömrü (anahtar bazında override edilebilir).</summary>
    TimeSpan DefaultDuration { get; set; }

    /// <summary>Fail-safe: kaynak (factory) hata verirse, süresi geçmiş değerin servis edilebileceği azami süre.</summary>
    TimeSpan FailSafeMaxDuration { get; set; }

    /// <summary>L1 (bellek) önbellek girdi sayısı tavanı (size-based eviction).</summary>
    long SizeLimit { get; set; }
}
