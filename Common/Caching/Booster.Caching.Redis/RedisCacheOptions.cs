using Booster.Caching.Abstraction;

namespace Booster.Caching.Redis;

/// <summary>
/// Redis + FusionCache (L1 bellek + L2 distributed + backplane) yapılandırması.
/// </summary>
public class RedisCacheOptions : ICacheOptions
{
    /// <summary>Cluster için "host1:port,host2:port" veya tekil "host:port".</summary>
    public string Endpoints { get; set; } = default!;
    public string? Password { get; set; }
    public int ConnectTimeout { get; set; } = 5000;
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>Bağlantı anlık koparsa exception fırlatılsın mı (false → dayanıklı).</summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>Tüm anahtarların önüne eklenen instance prefix'i.</summary>
    public string InstanceName { get; set; } = "Booster_";

    public bool Ssl { get; set; } = false;

    /// <summary>Varsayılan girdi ömrü (anahtar bazında override edilebilir).</summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Fail-safe: kaynak (factory) hata verirse, süresi geçmiş değerin servis edilebileceği azami süre.</summary>
    public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>L1 (bellek) önbellek girdi sayısı tavanı (size-based eviction).</summary>
    public long SizeLimit { get; set; } = 10000;
}
