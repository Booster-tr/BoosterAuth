using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Booster.Auth.Common.Internal.Amqp.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.NotificationService.Features._base.Entities;

/// <summary>
/// Bir bildirim tipinin şablonu. Anahtar + kanal + havuz birlikte tekildir: aynı olay
/// (ör. "password-reset") e-posta ve SMS için ayrı metinlere sahip olabilir.
///
/// <see cref="UserPoolId"/> boşsa şablon sistem geneli varsayılandır; doluysa yalnız o havuza
/// aittir ve varsayılanın yerine geçer. Havuz kendi metnini tanımlamadıysa varsayılana düşülür.
/// </summary>
public class NotificationTemplate : Entity, ITranslationParent<NotificationTemplateTranslation>
{
    public string Key { get; set; } = default!;

    public NotificationChannel Channel { get; set; }

    /// <summary>Havuza özel şablon; null ise sistem geneli varsayılan.</summary>
    public Guid? UserPoolId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<NotificationTemplateTranslation> Translations { get; set; } = [];
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Channel).HasConversion<int>();
        builder.Property(x => x.UserPoolId).IsRequired(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasMany(x => x.Translations)
               .WithOne(x => x.NotificationTemplate)
               .HasForeignKey(x => x.NotificationTemplateId)
               .OnDelete(DeleteBehavior.Cascade);

        // Postgres'te NULL'lar unique index içinde birbirinden farklı sayılır; varsayılan ve
        // havuza özel kayıtların tekilliği bu yüzden iki ayrı filtreli index ile korunur.
        builder.HasIndex(x => new { x.Key, x.Channel })
               .IsUnique()
               .HasDatabaseName("IX_NotificationTemplates_Key_Channel_Default")
               .HasFilter("\"UserPoolId\" IS NULL AND \"DeletedTime\" IS NULL");

        builder.HasIndex(x => new { x.Key, x.Channel, x.UserPoolId })
               .IsUnique()
               .HasDatabaseName("IX_NotificationTemplates_Key_Channel_UserPoolId")
               .HasFilter("\"UserPoolId\" IS NOT NULL AND \"DeletedTime\" IS NULL");
    }
}
