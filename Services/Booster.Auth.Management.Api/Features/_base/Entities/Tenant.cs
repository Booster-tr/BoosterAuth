using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class Tenant : Entity
{
    public string Name { get; set; } = default!;

    public virtual ICollection<UserPool> UserPools { get; set; } = [];
}

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();

        builder.HasMany(x => x.UserPools)
               .WithOne(x => x.Tenant)
               .HasForeignKey(w => w.TenantId)
               .OnDelete(DeleteBehavior.Cascade);

    }
}