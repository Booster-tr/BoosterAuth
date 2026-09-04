namespace Booster.Caching.Abstraction;

/// <summary>
/// Sağlayıcıdan bağımsız (provider-agnostic) önbellek sözleşmesi. Uygulama kodu yalnızca bu arayüze bağımlıdır;
/// </summary>
public interface ICacheService
{
    /// <summary>Değeri verilen anahtara yazar. <paramref name="tags"/> ile etiketlenip toplu invalidate edilebilir.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default);

    /// <summary>Anahtarı okur. Bulunamazsa <c>default(T)</c> döner (örn. <c>bool?</c> için <c>null</c>).</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Anahtarı okur; varlık bilgisini ayrıca döner (negatif/`default` değerleri ayırt etmek için).</summary>
    Task<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Anahtar yoksa <paramref name="factory"/> ile üretip yazar ve döner (cache-aside, stampede korumalı).</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default);

    /// <summary><see cref="GetOrSetAsync{T}(string, Func{Task{T}}, TimeSpan?, string[], CancellationToken)"/>'in CancellationToken alan fabrikalı sürümü.</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default);

    /// <summary>
    /// Factory'ye bir <see cref="CacheFactoryContext"/> verir; böylece üretilen sonuca göre bu girdinin ömrü
    /// (<c>ctx.Options.Duration</c>) DİNAMİK ayarlanabilir (ör. "bulunamadı" sonucunu kısa süre cache'lemek).
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CacheFactoryContext, Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default);

    /// <summary><see cref="CacheFactoryContext"/> + CancellationToken alan fabrikalı sürüm.</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CacheFactoryContext, CancellationToken, Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default);

    /// <summary>Senkron cache-aside.</summary>
    T GetOrSet<T>(string key, Func<T> factory, TimeSpan? expiry = null, string[]? tags = null);

    /// <summary>Tek anahtarı siler.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Verilen tag ile yazılmış tüm girdileri (instance'lar arası, backplane varsa) geçersiz kılar.</summary>
    Task RemoveByTagAsync(string tag, CancellationToken ct = default);

    /// <summary>Birden çok tag'i tek çağrıda geçersiz kılar.</summary>
    Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default);
}
