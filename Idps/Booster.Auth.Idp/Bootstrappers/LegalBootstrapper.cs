using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.LegalManagers;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Bootstrappers;

/// <summary>
/// Yasal metinleri varsayilan sablonlarla (LegalTemplates/*.md, gomulu kaynak) doldurur.
/// <list type="bullet">
/// <item>Eksik metin / dil eklenir.</item>
/// <item>Panelden DEGISTIRILMEMIS metin, sablon degistiyse yeni surume guncellenir (sablon iyilestirmeleri ulasir).</item>
/// <item>Degistirilmis metne ASLA dokunulmaz.</item>
/// </list>
/// </summary>
public sealed class LegalBootstrapper(IdpDbContext db, LegalContentService content, UiTextOverrides overrides)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var changed = false;

        foreach (var key in LegalDocumentKeys.All)
        {
            var document = await db.LegalDocuments.Include(d => d.Translations)
                .FirstOrDefaultAsync(d => d.Key == key, ct);

            if (document is null)
            {
                document = new LegalDocument { Key = key, CreatedTime = DateTimeOffset.UtcNow };
                document.SetId();
                db.LegalDocuments.Add(document);
                changed = true;
            }

            foreach (var language in LegalDocumentKeys.Languages)
            {
                var template = LegalTemplates.Load(key, language);
                if (template is null)
                    continue;

                var translation = document.Translations.FirstOrDefault(t => t.LanguageCode == language);
                if (translation is null)
                {
                    translation = new LegalDocumentTranslation { LanguageCode = language, CreatedTime = DateTimeOffset.UtcNow };
                    translation.SetId();
                    document.Translations.Add(translation);
                }
                else if (translation.Customized
                         || (translation.Title == template.Title && translation.Summary == template.Summary
                             && translation.Content == template.Content && translation.EffectiveDate == template.EffectiveDate))
                {
                    continue;
                }

                translation.Title = template.Title;
                translation.Summary = template.Summary;
                translation.Content = template.Content;
                translation.EffectiveDate = template.EffectiveDate;
                translation.Customized = false;
                translation.UpdatedTime = DateTimeOffset.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
            await content.InvalidateAsync(ct);
        }

        await overrides.ReloadAsync(ct);
    }
}
