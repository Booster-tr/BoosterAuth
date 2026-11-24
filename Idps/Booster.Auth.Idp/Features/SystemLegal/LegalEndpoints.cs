using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.LegalManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.SystemLegal;

// Yasal metinler ve kurum bilgileri: kurulum geneli, yalnizca panel yoneticisi (admin rolu).

internal static class LegalAuth
{
    public static void Admin(this EndpointDefinition ep) => ep.Options(x => x.RequireAuthorization(
        new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
        new RequireRoleAttribute(AuthConstants.AdminRoleName)));
}

// ----------------------------------------------------------------------------- liste

public record LegalDocumentLanguage(string LanguageCode, string Title, DateOnly EffectiveDate, bool Customized);

public record LegalDocumentListItem(string Key, IReadOnlyList<LegalDocumentLanguage> Languages);

public class GetLegalDocumentsEndpoint(IdpDbContext db) : EndpointWithoutRequest<IReadOnlyList<LegalDocumentListItem>>
{
    public override void Configure()
    {
        Get("system/legal/documents");
        Definition.Admin();
        Summary(s => s.Summary = "Giriş ekranındaki yasal metinler (gizlilik, kullanım koşulları, KVKK, açık rıza, çerez)");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var docs = await db.LegalDocuments.AsNoTracking().Include(d => d.Translations).ToListAsync(ct);

        var result = LegalDocumentKeys.All
            .Select(key => docs.FirstOrDefault(d => d.Key == key))
            .Where(d => d is not null)
            .Select(d => new LegalDocumentListItem(d!.Key, d.Translations
                .OrderBy(t => LegalDocumentKeys.Languages.ToList().IndexOf(t.LanguageCode))
                .Select(t => new LegalDocumentLanguage(t.LanguageCode, t.Title, t.EffectiveDate, t.Customized))
                .ToList()))
            .ToList();

        await Send.OkAsync(result, ct);
    }
}

// ----------------------------------------------------------------------------- tek metin

public record LegalDocumentDetail(
    string Key,
    string LanguageCode,
    string Title,
    string? Summary,
    string Content,
    DateOnly EffectiveDate,
    bool Customized,
    IReadOnlyList<string> Placeholders);

public class GetLegalDocumentEndpoint(IdpDbContext db) : EndpointWithoutRequest<LegalDocumentDetail>
{
    public override void Configure()
    {
        Get("system/legal/documents/{key}");
        Definition.Admin();
        Summary(s => s.Summary = "Yasal metnin bir dildeki içeriği (Markdown)");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var key = Route<string>("key")!;
        var language = LegalDocumentKeys.NormalizeLanguage(Query<string>("LanguageCode", isRequired: false));

        var t = await db.LegalDocumentTranslations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LegalDocument.Key == key && x.LanguageCode == language, ct)
            ?? throw new BusinessException(IdpMessageKeys.LegalDocumentNotFound);

        await Send.OkAsync(new LegalDocumentDetail(key, t.LanguageCode, t.Title, t.Summary, t.Content, t.EffectiveDate,
            t.Customized, LegalContentService.PlaceholderKeys), ct);
    }
}

public record UpdateLegalDocumentRequest(string LanguageCode, string Title, string? Summary, string Content, DateOnly EffectiveDate);

public class UpdateLegalDocumentEndpoint(IdpDbContext db, LegalContentService content) : Endpoint<UpdateLegalDocumentRequest, LegalDocumentDetail>
{
    public override void Configure()
    {
        Put("system/legal/documents/{key}");
        Definition.Admin();
        Summary(s =>
        {
            s.Summary = "Yasal metni günceller";
            s.Description = "Markdown; ham HTML işlenmez. {{organization}} gibi yer tutucular kurum bilgileriyle doldurulur.";
        });
    }

    public override async Task HandleAsync(UpdateLegalDocumentRequest req, CancellationToken ct)
    {
        var key = Route<string>("key")!;
        var language = LegalDocumentKeys.NormalizeLanguage(req.LanguageCode);

        var document = await db.LegalDocuments.Include(d => d.Translations).FirstOrDefaultAsync(d => d.Key == key, ct)
            ?? throw new BusinessException(IdpMessageKeys.LegalDocumentNotFound);

        var t = document.Translations.FirstOrDefault(x => x.LanguageCode == language);
        if (t is null)
        {
            t = new LegalDocumentTranslation { LanguageCode = language, CreatedTime = DateTimeOffset.UtcNow };
            t.SetId();
            document.Translations.Add(t);
        }

        t.Title = req.Title.Trim();
        t.Summary = string.IsNullOrWhiteSpace(req.Summary) ? null : req.Summary.Trim();
        t.Content = req.Content;
        t.EffectiveDate = req.EffectiveDate;
        t.Customized = true;
        t.UpdatedTime = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await content.InvalidateAsync(ct);

        await Send.OkAsync(new LegalDocumentDetail(key, language, t.Title, t.Summary, t.Content, t.EffectiveDate, true,
            LegalContentService.PlaceholderKeys), ct);
    }
}

public class UpdateLegalDocumentValidator : Validator<UpdateLegalDocumentRequest>
{
    public UpdateLegalDocumentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Summary).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Content).NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100_000).WithMessage(ValidationErrors.MaxLengthError);
    }
}

public record ResetLegalDocumentRequest(string LanguageCode);

public class ResetLegalDocumentEndpoint(IdpDbContext db, LegalContentService content) : Endpoint<ResetLegalDocumentRequest, LegalDocumentDetail>
{
    public override void Configure()
    {
        Post("system/legal/documents/{key}/reset");
        Definition.Admin();
        Summary(s => s.Summary = "Yasal metni varsayılan şablona döndürür");
    }

    public override async Task HandleAsync(ResetLegalDocumentRequest req, CancellationToken ct)
    {
        var key = Route<string>("key")!;
        var language = LegalDocumentKeys.NormalizeLanguage(req.LanguageCode);

        var template = LegalTemplates.Load(key, language) ?? throw new BusinessException(IdpMessageKeys.LegalDocumentNotFound);
        var t = await db.LegalDocumentTranslations
            .FirstOrDefaultAsync(x => x.LegalDocument.Key == key && x.LanguageCode == language, ct)
            ?? throw new BusinessException(IdpMessageKeys.LegalDocumentNotFound);

        t.Title = template.Title;
        t.Summary = template.Summary;
        t.Content = template.Content;
        t.EffectiveDate = template.EffectiveDate;
        t.Customized = false;
        t.UpdatedTime = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await content.InvalidateAsync(ct);

        await Send.OkAsync(new LegalDocumentDetail(key, language, t.Title, t.Summary, t.Content, t.EffectiveDate, false,
            LegalContentService.PlaceholderKeys), ct);
    }
}

// ----------------------------------------------------------------------------- kurum bilgileri

public record LegalProfileDto(
    string? OrganizationName,
    string? Address,
    string? Email,
    string? Phone,
    string? KepAddress,
    string? Website,
    string? RegistryInfo);

public class GetLegalProfileEndpoint(LegalContentService content) : EndpointWithoutRequest<LegalProfileDto>
{
    public override void Configure()
    {
        Get("system/legal/profile");
        Definition.Admin();
        Summary(s => s.Summary = "Yasal metinlerdeki veri sorumlusu (kurum) bilgileri");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await content.GetProfileAsync(ct);
        await Send.OkAsync(new LegalProfileDto(p.OrganizationName, p.Address, p.Email, p.Phone, p.KepAddress, p.Website, p.RegistryInfo), ct);
    }
}

public class UpdateLegalProfileEndpoint(IdpDbContext db, LegalContentService content) : Endpoint<LegalProfileDto, LegalProfileDto>
{
    public override void Configure()
    {
        Put("system/legal/profile");
        Definition.Admin();
        Summary(s => s.Summary = "Kurum bilgilerini günceller (metinlerdeki yer tutucular)");
    }

    public override async Task HandleAsync(LegalProfileDto req, CancellationToken ct)
    {
        var p = await db.LegalProfiles.FirstOrDefaultAsync(x => x.Id == LegalProfile.RowId, ct);
        if (p is null)
        {
            p = new LegalProfile { Id = LegalProfile.RowId, CreatedTime = DateTimeOffset.UtcNow };
            db.LegalProfiles.Add(p);
        }

        static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        p.OrganizationName = Clean(req.OrganizationName);
        p.Address = Clean(req.Address);
        p.Email = Clean(req.Email);
        p.Phone = Clean(req.Phone);
        p.KepAddress = Clean(req.KepAddress);
        p.Website = Clean(req.Website);
        p.RegistryInfo = Clean(req.RegistryInfo);
        p.UpdatedTime = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await content.InvalidateAsync(ct);

        await Send.OkAsync(req, ct);
    }
}

public class UpdateLegalProfileValidator : Validator<LegalProfileDto>
{
    public UpdateLegalProfileValidator()
    {
        RuleFor(x => x.OrganizationName).MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Address).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Email).MaximumLength(320).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Phone).MaximumLength(50).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.KepAddress).MaximumLength(320).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Website).MaximumLength(500).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.RegistryInfo).MaximumLength(500).WithMessage(ValidationErrors.MaxLengthError);
    }
}
