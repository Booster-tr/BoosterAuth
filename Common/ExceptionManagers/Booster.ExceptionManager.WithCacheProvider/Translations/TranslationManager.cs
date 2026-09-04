using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Translations;

namespace Booster.ExceptionManager.WithCacheProvider.Translations;

public class TranslationManager(ICacheService cacheService) : ITranslationManager
{
    public static string CacheKey(string language, string key) => $"trans:{language}:{key}";

    public async Task<string> GetAsync(string key, string language, CancellationToken ct = default)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        var normalizedLang = language.Trim().ToLowerInvariant();

        var value = await cacheService.GetAsync<string>(CacheKey(normalizedLang, normalizedKey), ct);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public void Set(string key, string language, string value)
    {
        throw new NotImplementedException();
    }
}
