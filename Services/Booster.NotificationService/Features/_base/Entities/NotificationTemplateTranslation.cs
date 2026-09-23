using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.NotificationService.Features._base.Entities;

public class NotificationTemplateTranslation : Entity, ITranslation
{
    public Guid NotificationTemplateId { get; set; }

    public virtual NotificationTemplate NotificationTemplate { get; set; } = default!;

    /// <summary>E-postada konu satırı; SMS/push'ta başlık. Boş bırakılabilir.</summary>
    public string? Subject { get; set; }

    /// <summary>Yer tutucular {{Key}} biçimindedir; olaydaki Data sözlüğünden doldurulur.</summary>
    public string Body { get; set; } = default!;

    public string LanguageCode { get; set; } = default!;
}

public class NotificationTemplateTranslationConfiguration : IEntityTypeConfiguration<NotificationTemplateTranslation>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateTranslation> builder)
    {
        builder.ToTable("NotificationTemplateTranslations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.NotificationTemplateId).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Body).HasMaxLength(20000).IsRequired();
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();

        builder.HasIndex(x => new { x.NotificationTemplateId, x.LanguageCode }).IsUnique();
    }
}
