using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

/// <summary>
/// Giris ekranlarinda son kullaniciya gosterilen yasal metin (gizlilik, kullanim kosullari, KVKK, acik riza, cerez).
/// Metinler UYGULAMA SAHIBINE (kurulumu isleten kurum, veri sorumlusu) aittir; kurulum genelidir ve panelin
/// Sistem menusunden duzenlenir. Ilk acilista tarafsiz sablonlarla doldurulur (LegalBootstrapper).
/// </summary>
public class LegalDocument : Entity, ITranslationParent<LegalDocumentTranslation>
{
    /// <summary><see cref="LegalDocumentKeys"/> degerlerinden biri.</summary>
    public string Key { get; set; } = default!;

    public virtual ICollection<LegalDocumentTranslation> Translations { get; set; } = [];
}

public class LegalDocumentTranslation : Entity, ITranslation
{
    public Guid LegalDocumentId { get; set; }

    public virtual LegalDocument LegalDocument { get; set; } = default!;

    public string LanguageCode { get; set; } = default!;

    public string Title { get; set; } = default!;

    /// <summary>Basligin altindaki kisa aciklama.</summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Markdown. HTML islenmez; guvenli olmayan baglantilar atilir (LegalMarkdown). Yer tutucular
    /// ({{organization}} vb.) gosterimde kurum bilgileriyle doldurulur (<see cref="LegalProfile"/>).
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>Metinde gosterilen yururluk / son guncelleme tarihi.</summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Panelden degistirildi mi (false = varsayilan sablon).</summary>
    public bool Customized { get; set; }
}

public static class LegalDocumentKeys
{
    public const string Privacy = "privacy";
    public const string Terms = "terms";
    public const string Kvkk = "kvkk";
    public const string ExplicitConsent = "explicit-consent";
    public const string Cookies = "cookies";

    public static readonly IReadOnlyList<string> All = [Privacy, Terms, Kvkk, ExplicitConsent, Cookies];

    public static readonly IReadOnlyList<string> Languages = ["tr-tr", "en-us"];

    /// <summary>"en", "en-US", "tr" gibi degerleri desteklenen dillerden birine indirger.</summary>
    public static string NormalizeLanguage(string? language) =>
        language is not null && language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-us" : "tr-tr";

    /// <summary>Yazma uclari icin: "tr", "tr-TR", "en", "en-US" kabul edilir; baska dil (or. "de-de") reddedilir.
    /// Once her bilinmeyen dil "tr-tr"ye inip Turkce metnin uzerine yaziyordu.</summary>
    public static bool IsSupportedLanguage(string? language)
        => language is not null && (language.Equals("tr", StringComparison.OrdinalIgnoreCase) || language.Equals("en", StringComparison.OrdinalIgnoreCase)
            || Languages.Contains(language.ToLowerInvariant()));
}

public class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("LegalDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasMany(x => x.Translations)
               .WithOne(x => x.LegalDocument)
               .HasForeignKey(x => x.LegalDocumentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LegalDocumentTranslationConfiguration : IEntityTypeConfiguration<LegalDocumentTranslation>
{
    public void Configure(EntityTypeBuilder<LegalDocumentTranslation> builder)
    {
        builder.ToTable("LegalDocumentTranslations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(1000);
        builder.Property(x => x.Content).HasMaxLength(100_000).IsRequired();
        builder.HasIndex(x => new { x.LegalDocumentId, x.LanguageCode }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
    }
}
