using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class RoleScope : Entity
{
    public Guid RoleId { get; set; }

    public virtual Role Role { get; set; } = default!;

    public string Scope { get; set; } = default!;
}

public class RoleScopeConfiguration : IEntityTypeConfiguration<RoleScope>
{
    public void Configure(EntityTypeBuilder<RoleScope> builder)
    {
        builder.ToTable("RoleScopes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.RoleId).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.RoleId, x.Scope })
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");
    }
}