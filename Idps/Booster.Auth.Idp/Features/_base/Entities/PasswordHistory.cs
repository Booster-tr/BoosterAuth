using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Booster.Auth.Idp.Features._base.Entities;

public class PasswordHistory : IsolatedEntity
{
    public Guid PrincipalId { get; set; }

    [JsonIgnore] public virtual Principal Principal { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;
}

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> builder)
    {
        builder.ToTable("PasswordHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);
        builder.Property(x => x.IsolationId).IsRequired();

        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.Principal)
               .WithMany()
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PrincipalId, x.CreatedTime })
               .IsDescending(false, true);
    }
}
