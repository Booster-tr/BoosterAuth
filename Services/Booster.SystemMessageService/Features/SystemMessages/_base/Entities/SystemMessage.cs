using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.SystemMessageService.Features.SystemMessages._base.Entities;

public class SystemMessage : Entity, ITranslationParent<SystemMessageTranslation>
{
    public string Key { get; set; } = default!;

    public virtual ICollection<SystemMessageTranslation> Translations { get; set; } = [];
}


public class SystemMessageConfiguration : IEntityTypeConfiguration<SystemMessage>
{
    public void Configure(EntityTypeBuilder<SystemMessage> builder)
    {
        builder.ToTable("SystemMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);
        // 50 dar kaliyordu: authn.token_scope_policy_required_permissions_required
        // 54 karakter. Kisa sinir, anahtar uzadiginda seed'i calisma aninda patlatir.
        builder.Property(x => x.Key).HasMaxLength(150).IsRequired();

        builder.HasMany(x => x.Translations)
               .WithOne(x => x.SystemMessage)
               .HasForeignKey(x => x.SystemMessageId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.Key);

        
    }
}
