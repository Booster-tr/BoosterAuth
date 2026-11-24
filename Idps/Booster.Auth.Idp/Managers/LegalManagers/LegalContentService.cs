using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Caching.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Managers.LegalManagers;

/// <summary>Giris ekraninda gosterilecek, yer tutuculari doldurulmus ve HTML'e cevrilmis yasal metin.</summary>
public sealed record LegalPage(
    string Key,
    string LanguageCode,
    string Title,
    string? Summary,
    DateOnly EffectiveDate,
    string Html,
    IReadOnlyList<LegalMarkdown.Heading> Headings);

/// <summary>Yasal metinler ve kurum bilgileri: okuma (onbellekli) ve degisiklik sonrasi onbellek temizligi.</summary>
public sealed class LegalContentService(IdpDbContext db, ICacheService cache)
{
    public const string CacheTag = "legal";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>Dilde metin yoksa diger dile duser; hic yoksa null.</summary>
    public Task<LegalPage?> GetPageAsync(string key, string? language, CancellationToken ct)
    {
        var lang = LegalDocumentKeys.NormalizeLanguage(language);
        return cache.GetOrSetAsync<LegalPage?>($"legal:page:{key}:{lang}", async token =>
        {
            var translations = await db.LegalDocumentTranslations.AsNoTracking()
                .Where(t => t.LegalDocument.Key == key && t.LegalDocument.DeletedTime == null)
                .ToListAsync(token);

            var t = translations.FirstOrDefault(x => x.LanguageCode == lang) ?? translations.FirstOrDefault();
            if (t is null)
                return null;

            var placeholders = Placeholders(await GetProfileAsync(token), t.LanguageCode);
            var rendered = LegalMarkdown.Render(t.Content, placeholders);

            return new LegalPage(key, t.LanguageCode,
                LegalMarkdown.ReplacePlain(t.Title, placeholders),
                t.Summary is null ? null : LegalMarkdown.ReplacePlain(t.Summary, placeholders),
                t.EffectiveDate, rendered.Html, rendered.Headings);
        }, expiry: Ttl, tags: [CacheTag], ct: ct);
    }

    public async Task<LegalProfile> GetProfileAsync(CancellationToken ct) =>
        await db.LegalProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == LegalProfile.RowId, ct)
        ?? new LegalProfile { Id = LegalProfile.RowId };

    public Task InvalidateAsync(CancellationToken ct) => cache.RemoveByTagAsync(CacheTag, ct);

    /// <summary>Metinlerde kullanilabilen yer tutucular. Bos alanlar "[...]" ile gosterilir ki eksik oldugu gorulsun.</summary>
    public static IReadOnlyDictionary<string, string> Placeholders(LegalProfile p, string language)
    {
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        string Or(string? value, string tr, string english) => string.IsNullOrWhiteSpace(value) ? (en ? english : tr) : value.Trim();

        return new Dictionary<string, string>
        {
            ["organization"] = Or(p.OrganizationName, "[Kurum unvanı]", "[Organization name]"),
            ["address"] = Or(p.Address, "[Adres]", "[Address]"),
            ["email"] = Or(p.Email, "[E-posta adresi]", "[E-mail address]"),
            ["phone"] = Or(p.Phone, "[Telefon]", "[Phone]"),
            ["kep"] = Or(p.KepAddress, "[KEP adresi]", "[Registered e-mail (KEP)]"),
            ["website"] = Or(p.Website, "[Web sitesi]", "[Website]"),
            ["registry"] = Or(p.RegistryInfo, "[Sicil / MERSİS bilgisi]", "[Registry information]"),
        };
    }

    public static readonly IReadOnlyList<string> PlaceholderKeys =
        ["organization", "address", "email", "phone", "kep", "website", "registry"];
}
