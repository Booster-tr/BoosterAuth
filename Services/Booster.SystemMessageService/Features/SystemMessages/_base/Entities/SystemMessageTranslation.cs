using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.SystemMessageService.Features.SystemMessages._base.Entities;

public class SystemMessageTranslation : Entity, ITranslation
{
    public Guid SystemMessageId { get; set; }
    public virtual SystemMessage SystemMessage { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string LanguageCode { get; set; } = default!;
}

public class SystemMessageTranslationConfiguration : IEntityTypeConfiguration<SystemMessageTranslation>
{
    public void Configure(EntityTypeBuilder<SystemMessageTranslation> builder)
    {
        builder.ToTable("SystemMessageTranslations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);
        builder.Property(x => x.SystemMessageId);
        builder.Property(x => x.Title).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();

        builder.HasIndex(x => new { x.SystemMessageId, x.LanguageCode }).IsUnique();

        
    }
}
