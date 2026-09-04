using Booster.Caching.Abstraction;
using ZiggyCreatures.Caching.Fusion;

namespace Booster.Caching.Memory;

/// <summary>
/// <see cref="ICacheService"/>'in salt-bellek implementasyonu.
/// </summary>
public class MemoryFusionCacheService(IFusionCache fusionCache) : ICacheService
{
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default)
        => await fusionCache.SetAsync(key, value, opt => Apply(opt, expiry), tags, ct);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var result = await fusionCache.TryGetAsync<T>(key, token: ct);
        return result.HasValue ? result.Value : default;
    }

    public async Task<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken ct = default)
    {
        var result = await fusionCache.TryGetAsync<T>(key, token: ct);
        return result.HasValue ? (true, result.Value) : (false, default);
    }

    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default)
        => fusionCache.GetOrSetAsync<T>(key, async (_, _) => await factory(), opt => Apply(opt, expiry), tags, ct).AsTask();

    public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default)
        => fusionCache.GetOrSetAsync<T>(key, async (_, token) => await factory(token), opt => Apply(opt, expiry), tags, ct).AsTask();

    public Task<T> GetOrSetAsync<T>(string key, Func<CacheFactoryContext, Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default)
        => fusionCache.GetOrSetAsync<T>(key, async (fctx, _) =>
        {
            var c = new CacheFactoryContext();
            var value = await factory(c);
            ApplyFactoryContext(fctx, c);
            return value;
        }, opt => Apply(opt, expiry), tags, ct).AsTask();

    public Task<T> GetOrSetAsync<T>(string key, Func<CacheFactoryContext, CancellationToken, Task<T>> factory, TimeSpan? expiry = null, string[]? tags = null, CancellationToken ct = default)
        => fusionCache.GetOrSetAsync<T>(key, async (fctx, token) =>
        {
            var c = new CacheFactoryContext();
            var value = await factory(c, token);
            ApplyFactoryContext(fctx, c);
            return value;
        }, opt => Apply(opt, expiry), tags, ct).AsTask();

    public T GetOrSet<T>(string key, Func<T> factory, TimeSpan? expiry = null, string[]? tags = null)
        => fusionCache.GetOrSet<T>(key, _ => factory(), opt => Apply(opt, expiry), tags);

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => fusionCache.RemoveAsync(key, token: ct).AsTask();

    public Task RemoveByTagAsync(string tag, CancellationToken ct = default)
        => fusionCache.RemoveByTagAsync(tag, token: ct).AsTask();

    public async Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default)
    {
        foreach (var tag in tags)
            await fusionCache.RemoveByTagAsync(tag, token: ct);
    }

    private static void Apply(FusionCacheEntryOptions options, TimeSpan? expiry)
    {
        if (expiry.HasValue)
            options.Duration = expiry.Value;
        options.SetSize(1);
    }

    /// <summary>Factory'nin nötr context'te belirlediği dinamik ayarları FusionCache girdi seçeneklerine eşler.</summary>
    private static void ApplyFactoryContext<T>(FusionCacheFactoryExecutionContext<T> fctx, CacheFactoryContext context)
    {
        if (context.Options.Duration.HasValue)
            fctx.Options.Duration = context.Options.Duration.Value;
    }
}
