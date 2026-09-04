using Booster.ExceptionManager.Abstraction.Translations;

namespace Booster.ExceptionManager.LocalDict.Translations;

public class TranslationContext : ITranslationManager
{
    public static Dictionary<string, string> Translations { get; set; } = [];

    public async Task<string> GetAsync(string key, string language, CancellationToken ct = default)
    {
        return GetTranslation(key, language);
    }

    public string GetTranslation(string key, string language)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        var normalizedLang = language.Trim().ToLowerInvariant();
        return Translations[$"{normalizedKey}.{normalizedLang}"] ?? key;
    }

    public void Set(string key, string language, string value)
    {
        SetTranslation(key, language, value);
    }

    public void SetTranslation(string key, string language, string value)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        var normalizedLang = language.Trim().ToLowerInvariant();
        Translations[$"{normalizedKey}.{normalizedLang}"] = value;
    }
}

