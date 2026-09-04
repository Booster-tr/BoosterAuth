namespace Booster.Caching.Abstraction;

/// <summary>
/// Factory yürütme bağlamı. Üretilen sonuca göre BU girdinin önbellek ayarlarını (ör. ömür) DİNAMİK belirlemek için
/// kullanılır (örn. "bulunamadı" sonucunu kısa süre cache'lemek). Sağlayıcıdan bağımsızdır — FusionCache vb. sızmaz;
/// somut sağlayıcı bu ayarları kendi modeline eşler.
/// </summary>
public sealed class CacheFactoryContext
{
    /// <summary>Bu girdiye özel önbellek ayarları.</summary>
    public CacheEntryOptions Options { get; } = new();

    public CacheFactoryContext SetTags(params string[] tags)
    {
        Options.Tags = tags;
        return this;
    }

    public CacheFactoryContext SetDuration(TimeSpan duration)
    {
        Options.Duration = duration;
        return this;
    }
}

/// <summary>Tek bir önbellek girdisinin ayarları.</summary>
public sealed class CacheEntryOptions
{
    /// <summary>Bu girdi için ömür. <c>null</c> → çağrıdaki <c>expiry</c> / sağlayıcı varsayılanı geçerli.</summary>
    public TimeSpan? Duration { get; set; }

    public string[]? Tags { get; set; }
}
