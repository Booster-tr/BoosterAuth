using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

public class PrincipalScope : Entity, IIsolatedEntity
{
    public Guid IsolationId { get; set; }
    public Guid PrincipalId { get; set; }

    public string Scope { get; set; } = default!;
}

public class PrincipalScopeConfiguration : IEntityTypeConfiguration<PrincipalScope>
{
    public void Configure(EntityTypeBuilder<PrincipalScope> builder)
    {
        builder.ToTable("PrincipalScopes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => new { x.PrincipalId, x.Scope }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.HasIndex(x => x.PrincipalId);

        builder.HasOne<Principal>()
               .WithMany()
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
