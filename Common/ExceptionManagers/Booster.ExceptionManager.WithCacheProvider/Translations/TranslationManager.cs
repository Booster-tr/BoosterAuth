using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Translations;

namespace Booster.ExceptionManager.WithCacheProvider.Translations;

public class TranslationManager(ICacheService cacheService) : ITranslationManager
{
    public static string CacheKey(string language, string key) => $"trans:{language}:{key}";

    /// <summary>
    /// Önbellekteki değer <c>Başlık|Mesaj</c> biçiminde tutulur (SystemMessage
    /// servisi böyle yazar). Ayrıştırılmadan döndürülseydi kullanıcı ekranda
    /// "Parola|Parola çok kısa." gibi ham bir dize görürdü.
    /// </summary>
    private const char TitleSeparator = '|';

    public async Task<string> GetAsync(string key, string language, CancellationToken ct = default)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        var normalizedLang = language.Trim().ToLowerInvariant();

        var value = await cacheService.GetAsync<string>(CacheKey(normalizedLang, normalizedKey), ct);

        // Karşılığı yoksa anahtarın kendisi döner: çeviri eksiğinin ekranda
        // görünmesi, sessizce boş metin göstermekten iyidir.
        if (string.IsNullOrEmpty(value))
            return key;

        var separatorIndex = value.IndexOf(TitleSeparator);

        return separatorIndex >= 0 ? value[(separatorIndex + 1)..] : value;
    }

    public void Set(string key, string language, string value)
    {
        throw new NotImplementedException();
    }
}
