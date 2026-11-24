using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

/// <summary>
/// Giris ekranlarindaki bir yazinin kurulum geneli degisikligi. Anahtar, IdP kaynak dosyasindaki (SharedResource)
/// anahtardir; dilde bir deger varsa kaynak dosyadaki metnin yerine gecer (OverridableStringLocalizer).
/// </summary>
public class UiText : Entity, ITranslationParent<UiTextTranslation>
{
    public string Key { get; set; } = default!;

    public virtual ICollection<UiTextTranslation> Translations { get; set; } = [];
}

public class UiTextTranslation : Entity, ITranslation
{
    public Guid UiTextId { get; set; }

    public virtual UiText UiText { get; set; } = default!;

    public string LanguageCode { get; set; } = default!;

    /// <summary>Duz metin; &lt; ve &gt; kabul edilmez (bazi anahtarlar HTML icine yerlestirilir).</summary>
    public string Value { get; set; } = default!;
}

public class UiTextConfiguration : IEntityTypeConfiguration<UiText>
{
    public void Configure(EntityTypeBuilder<UiText> builder)
    {
        builder.ToTable("UiTexts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasMany(x => x.Translations)
               .WithOne(x => x.UiText)
               .HasForeignKey(x => x.UiTextId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UiTextTranslationConfiguration : IEntityTypeConfiguration<UiTextTranslation>
{
    public void Configure(EntityTypeBuilder<UiTextTranslation> builder)
    {
        builder.ToTable("UiTextTranslations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.UiTextId, x.LanguageCode }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
    }
}
