using Booster.Database.Postgres.Models;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.NotificationService.Features._base.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.NotificationService.Features._base.Entities;

/// <summary>
/// Tek bir gönderim denemesinin kaydı. Şablon doldurulduktan sonraki NİHAİ metni tutar;
/// şablon sonradan değişse bile gönderilenin ne olduğu izlenebilir kalır.
/// </summary>
public class Notification : Entity
{
    public Guid TenantId { get; set; }

    public Guid UserPoolId { get; set; }

    public Guid? PrincipalId { get; set; }

    public string TemplateKey { get; set; } = default!;

    public NotificationChannel Channel { get; set; }

    public string Target { get; set; } = default!;

    public string LanguageCode { get; set; } = default!;

    public string? Subject { get; set; }

    public string Body { get; set; } = default!;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public int AttemptCount { get; set; }

    public string? FailureMessage { get; set; }

    public DateTimeOffset? SentTime { get; set; }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserPoolId).IsRequired();
        builder.Property(x => x.PrincipalId).IsRequired(false);
        builder.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Channel).HasConversion<int>();
        // Target ve Body SIFRELI saklanir (AES-GCM, NotificationRequestedEventConsumer): alici adresi, kod, davet baglantisi.
        builder.Property(x => x.Target).HasMaxLength(500).IsRequired();
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Body).HasMaxLength(20000).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(NotificationStatus.Pending);
        builder.Property(x => x.FailureMessage).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.SentTime).IsRequired(false);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.PrincipalId);
        builder.HasIndex(x => new { x.Status, x.Channel });
    }
}
