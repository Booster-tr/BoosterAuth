using System.Globalization;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Booster.Auth.Idp.Managers.LegalManagers;

/// <summary>
/// Ekran yazisi degisiklikleri (UiText) icin bellek ici anlik goruntu. IStringLocalizer senkron oldugundan her
/// cagri veritabanina gitmez: goruntu en fazla <see cref="RefreshAfter"/> eskir ve arka planda tazelenir; panelden
/// kaydedilince ayni sunucuda hemen yenilenir (birden cok pod'da digerleri en gec bir dakikada gorur).
/// </summary>
public sealed class UiTextOverrides(IServiceScopeFactory scopeFactory, ILogger<UiTextOverrides> logger)
{
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(1);

    private volatile IReadOnlyDictionary<string, string> _values = new Dictionary<string, string>();
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private int _refreshing;

    public string? Find(string key, CultureInfo culture)
    {
        // Eskiden her anahtar degistirilebiliyordu; o donemden kalan kayitlar artik uygulanmaz (silinmez).
        if (!Features.SystemLegal.UiTextDefaults.IsCustomizable(key))
            return null;

        if (DateTimeOffset.UtcNow - _loadedAt > RefreshAfter && Interlocked.Exchange(ref _refreshing, 1) == 0)
            _ = Task.Run(async () =>
            {
                try { await ReloadAsync(CancellationToken.None); }
                catch (Exception ex) { logger.LogWarning(ex, "Ekran yazisi degisiklikleri yuklenemedi."); }
                finally { Interlocked.Exchange(ref _refreshing, 0); }
            });

        return _values.TryGetValue(Compose(LegalDocumentKeys.NormalizeLanguage(culture.Name), key), out var value) ? value : null;
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdpDbContext>();

        var rows = await db.UiTextTranslations.AsNoTracking()
            .Where(t => t.UiText.DeletedTime == null)
            .Select(t => new { t.UiText.Key, t.LanguageCode, t.Value })
            .ToListAsync(ct);

        _values = rows.ToDictionary(r => Compose(r.LanguageCode, r.Key), r => r.Value, StringComparer.Ordinal);
        _loadedAt = DateTimeOffset.UtcNow;
    }

    private static string Compose(string language, string key) => language + "\u001f" + key;
}

/// <summary>
/// Gorunumlerdeki <c>T</c> (IStringLocalizer&lt;SharedResource&gt;): once kurulumun degisikligine, yoksa kaynak
/// dosyaya bakar. Bicim parametreli anahtarlarda ({0}) degisiklik de bicimlenir.
/// </summary>
public sealed class OverridableStringLocalizer(IStringLocalizerFactory factory, UiTextOverrides overrides)
    : IStringLocalizer<SharedResource>
{
    private readonly IStringLocalizer _inner = factory.Create(typeof(SharedResource));

    public LocalizedString this[string name]
        => overrides.Find(name, CultureInfo.CurrentUICulture) is { } value
            ? new LocalizedString(name, value, resourceNotFound: false)
            : _inner[name];

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            if (overrides.Find(name, CultureInfo.CurrentUICulture) is not { } value)
                return _inner[name, arguments];
            try
            {
                return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, value, arguments), resourceNotFound: false);
            }
            catch (FormatException)
            {
                return _inner[name, arguments];
            }
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _inner.GetAllStrings(includeParentCultures);
}
