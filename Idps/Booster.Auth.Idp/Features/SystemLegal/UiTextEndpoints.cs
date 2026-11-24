using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.LegalManagers;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.SystemLegal;

// Giris ekranlarindaki tanitim yazilari (UiText): yalnizca UiTextDefaults.IsCustomizable anahtarlari kurulum genelinde
// degistirilebilir. Buton, hata, cerez cubugu gibi standart yazilar kaynak dosyada sabittir.

public record UiTextItem(string Key, string DefaultTr, string DefaultEn, string? Tr, string? En);

public class GetUiTextsEndpoint(IdpDbContext db) : EndpointWithoutRequest<IReadOnlyList<UiTextItem>>
{
    public override void Configure()
    {
        Get("system/ui-texts");
        Definition.Admin();
        Summary(s => s.Summary = "Giriş ekranı yazıları: varsayılanlar ve kurulumdaki değişiklikler");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var overrides = await db.UiTextTranslations.AsNoTracking()
            .Where(t => t.UiText.DeletedTime == null)
            .Select(t => new { t.UiText.Key, t.LanguageCode, t.Value })
            .ToListAsync(ct);

        string? Find(string key, string lang) => overrides.FirstOrDefault(o => o.Key == key && o.LanguageCode == lang)?.Value;

        var tr = UiTextDefaults.For("tr-tr");
        var en = UiTextDefaults.For("en-us");

        var items = tr.Keys.Union(en.Keys)
            .Where(UiTextDefaults.IsCustomizable)
            .Order(StringComparer.Ordinal)
            .Select(k => new UiTextItem(k, tr.GetValueOrDefault(k, ""), en.GetValueOrDefault(k, ""), Find(k, "tr-tr"), Find(k, "en-us")))
            .ToList();

        await Send.OkAsync(items, ct);
    }
}

public record SetUiTextRequest(string Key, string LanguageCode, string Value);

public class SetUiTextEndpoint(IdpDbContext db, UiTextOverrides cache) : Endpoint<SetUiTextRequest>
{
    public override void Configure()
    {
        Put("system/ui-texts");
        Definition.Admin();
        Summary(s =>
        {
            s.Summary = "Bir giriş ekranı yazısını değiştirir";
            s.Description = "Düz metin; < ve > kabul edilmez. Varsayılandaki {0} gibi yer tutucular korunmalıdır.";
        });
    }

    public override async Task HandleAsync(SetUiTextRequest req, CancellationToken ct)
    {
        var language = LegalDocumentKeys.NormalizeLanguage(req.LanguageCode);
        var defaults = UiTextDefaults.For(language);

        if (!UiTextDefaults.IsCustomizable(req.Key) || !defaults.TryGetValue(req.Key, out var defaultValue))
            throw new BusinessException(IdpMessageKeys.UiTextKeyUnknown);
        if (!UiTextDefaults.SameFormatArguments(defaultValue, req.Value))
            throw new BusinessException(IdpMessageKeys.UiTextInvalid);

        var text = await db.UiTexts.Include(u => u.Translations).FirstOrDefaultAsync(u => u.Key == req.Key, ct);
        if (text is null)
        {
            text = new UiText { Key = req.Key, CreatedTime = DateTimeOffset.UtcNow };
            text.SetId();
            db.UiTexts.Add(text);
        }

        var t = text.Translations.FirstOrDefault(x => x.LanguageCode == language);
        if (t is null)
        {
            t = new UiTextTranslation { LanguageCode = language, CreatedTime = DateTimeOffset.UtcNow };
            t.SetId();
            text.Translations.Add(t);
        }

        t.Value = req.Value.Trim();
        t.UpdatedTime = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await cache.ReloadAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public class SetUiTextValidator : Validator<SetUiTextRequest>
{
    public SetUiTextValidator()
    {
        RuleFor(x => x.LanguageCode).Must(LegalDocumentKeys.IsSupportedLanguage).WithMessage(IdpMessageKeys.LanguageNotSupported);
        RuleFor(x => x.Key).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Value).NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(4000).WithMessage(ValidationErrors.MaxLengthError)
            // Bazi anahtarlar HTML icine yerlestirilir; isaretleme ile betik sokulamasin.
            .Must(v => v is null || (!v.Contains('<') && !v.Contains('>'))).WithMessage(IdpMessageKeys.UiTextInvalid);
    }
}

public class ResetUiTextEndpoint(IdpDbContext db, UiTextOverrides cache) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("system/ui-texts");
        Definition.Admin();
        Summary(s => s.Summary = "Bir giriş ekranı yazısını varsayılana döndürür");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var key = Query<string>("Key")!;
        var language = LegalDocumentKeys.NormalizeLanguage(Query<string>("LanguageCode", isRequired: false));

        var text = await db.UiTexts.Include(u => u.Translations).FirstOrDefaultAsync(u => u.Key == key, ct);
        var t = text?.Translations.FirstOrDefault(x => x.LanguageCode == language);
        if (t is not null)
        {
            db.UiTextTranslations.Remove(t);
            if (text!.Translations.Count == 1)
                db.UiTexts.Remove(text);
            await db.SaveChangesAsync(ct);
            await cache.ReloadAsync(ct);
        }

        await Send.NoContentAsync(ct);
    }
}

/// <summary>Kaynak dosyadaki (SharedResource) varsayilan yazilar.</summary>
public static partial class UiTextDefaults
{
    private static readonly ResourceManager Resources =
        new("Booster.Auth.Idp.Resources.SharedResource", typeof(SharedResource).Assembly);

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Cache = new();
    private static readonly Lock Gate = new();

    public static IReadOnlyDictionary<string, string> For(string language)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(language, out var cached))
                return cached;

            var culture = CultureInfo.GetCultureInfo(language == "en-us" ? "en-US" : "tr-TR");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            // Turkce notr kaynaktir; Ingilizce uydu derlemede eksik anahtar notre duser.
            foreach (var set in new[] { Resources.GetResourceSet(CultureInfo.InvariantCulture, true, true), Resources.GetResourceSet(culture, true, true) })
            {
                if (set is null) continue;
                foreach (DictionaryEntry e in set)
                    if (e.Key is string k && e.Value is string v)
                        values[k] = v;
            }

            return Cache[language] = values;
        }
    }

    /// <summary>
    /// Kurulumun degistirebilecegi anahtarlar: giris ekranlarinin tanitim paneli (ust baslik, her ekranin baslik / aciklamasi)
    /// ve marka adi gecen guvenlik notu. Musteri kendi kurum diliyle ve markasiyla anlatabilsin diye. Digerleri standarttir.
    /// <c>Layout.Footnote</c> ("Bu sayfa Booster Auth tarafindan sunulmaktadir") bilincli olarak DISARIDADIR.
    /// </summary>
    public static bool IsCustomizable(string key) =>
        key is "Layout.Eyebrow" or "Layout.SecureNote"
        || key.EndsWith(".BrandHeadline", StringComparison.Ordinal)
        || key.EndsWith(".BrandLead", StringComparison.Ordinal);

    /// <summary>Degisiklik varsayilandaki {0}, {1} ... yer tutucularinin aynisini icermeli.</summary>
    public static bool SameFormatArguments(string defaultValue, string value)
    {
        var expected = Arguments(defaultValue);
        if (!expected.SetEquals(Arguments(value)))
            return false;
        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, value, new object[expected.Count == 0 ? 0 : expected.Max() + 1]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HashSet<int> Arguments(string text) =>
        FormatArgument().Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToHashSet();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex FormatArgument();
}
