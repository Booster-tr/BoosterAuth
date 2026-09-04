using Booster.Caching.Abstraction;

namespace Booster.Caching.Memory;

/// <summary>
/// Salt-bellek (L1) yapılandırması. Tek-instance senaryolar veya distributed katman istenmeyen
/// servisler için. invalidation yalnızca yerel süreçte geçerlidir.
/// </summary>
public class MemoryCacheOptions : ICacheOptions
{
    /// <summary>Varsayılan girdi ömrü.</summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Fail-safe için süresi geçmiş değerin servis edilebileceği azami süre.</summary>
    public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>L1 önbellek girdi sayısı tavanı.</summary>
    public long SizeLimit { get; set; } = 10000;
}
