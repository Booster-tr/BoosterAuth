namespace Booster.ExceptionManager.Abstraction.Translations;

public interface ITranslationManager
{
    Task<string> GetAsync(string key, string language, CancellationToken ct = default);

    /// <summary>
    /// Only Use Dict Translation
    /// </summary>
    /// <param name="key"></param>
    /// <param name="language"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    void Set(string key, string language,string value);
}
