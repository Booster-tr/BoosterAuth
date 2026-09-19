using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class Role : Entity
{
    public Guid UserPoolId { get; set; }

    public string Key { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    public virtual UserPool UserPool { get; set; } = default!;
    public virtual ICollection<RoleScope> RoleScopes { get; set; } = [];
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.UserPoolId).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired(false);

        builder.HasIndex(x => x.UserPoolId).IsUnique(false);

        builder.HasIndex(x => new { x.UserPoolId, x.Key })
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");

        builder.HasOne(x => x.UserPool)
               .WithMany(w => w.Roles)
               .HasForeignKey(x => x.UserPoolId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RoleScopes)
               .WithOne(x => x.Role)
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}