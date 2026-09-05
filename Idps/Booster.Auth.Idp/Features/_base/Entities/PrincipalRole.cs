using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

public class PrincipalRole : Entity, IIsolatedEntity
{
    public Guid IsolationId { get; set; }
    public Guid PrincipalId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = default!;
}

public class PrincipalRoleConfiguration : IEntityTypeConfiguration<PrincipalRole>
{
    public void Configure(EntityTypeBuilder<PrincipalRole> builder)
    {
        builder.ToTable("PrincipalRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.RoleId).IsRequired();
        builder.Property(x => x.RoleName).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.PrincipalId, x.RoleId }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.HasIndex(x => x.PrincipalId);

        builder.HasOne<Principal>()
               .WithMany()
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
