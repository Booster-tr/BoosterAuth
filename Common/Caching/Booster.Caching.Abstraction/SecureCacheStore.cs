using System.Security.Cryptography;
using System.Text;

namespace Booster.Caching.Abstraction;

/// <summary>
/// <see cref="ICacheService"/> üzerine kurulu, sağlayıcıdan bağımsız güvenli veri saklama soyutlaması. Ham anahtar
/// değil SHA-256 hash'i cache key olur; tip başına ayrı namespace (typeof(TData).Name). İki saklama biçimi:
/// anahtarı store üretir (rastgele tek-kullanımlık token — authorization code, federation state, reset/verify token)
/// veya çağıran belirler (deterministik — ör. pending-link {poolId}:{principalId}). Get/Consume/Remove ortaktır.
/// </summary>
public interface ISecureCacheStore<TData> where TData : class
{
    Task<string> StoreAsync(TData data, TimeSpan ttl, CancellationToken ct = default);

    Task StoreAsync(string key, TData data, TimeSpan ttl, CancellationToken ct = default);

    Task<TData?> GetAsync(string key, CancellationToken ct = default);

    Task<TData?> ConsumeAsync(string key, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
}

public class SecureCacheStore<TData>(ICacheService cacheService) : ISecureCacheStore<TData>
    where TData : class
{
    private static string CacheKey(string key)
        => $"securecache:{typeof(TData).Name}:{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)))}";

    public async Task<string> StoreAsync(TData data, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = GenerateKey();
        await StoreAsync(key, data, ttl, ct);
        return key;
    }

    public Task StoreAsync(string key, TData data, TimeSpan ttl, CancellationToken ct = default)
        => cacheService.SetAsync(CacheKey(key), data, ttl, ct: ct);

    public Task<TData?> GetAsync(string key, CancellationToken ct = default)
        => cacheService.GetAsync<TData>(CacheKey(key), ct);

    public async Task<TData?> ConsumeAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(key);
        var data = await cacheService.GetAsync<TData>(cacheKey, ct);
        if (data is not null)
            await cacheService.RemoveAsync(cacheKey, ct);
        return data;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => cacheService.RemoveAsync(CacheKey(key), ct);

    private static string GenerateKey()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
