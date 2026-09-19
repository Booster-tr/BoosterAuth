using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Z.Features._base.Entities;

public class PoolStore : Entity
{
    public Guid PoolId { get; set; }

    public string StoreId { get; set; } = default!;

    public string ActiveModelId { get; set; } = default!;
}

public class PoolStoreConfiguration : IEntityTypeConfiguration<PoolStore>
{
    public void Configure(EntityTypeBuilder<PoolStore> builder)
    {
        builder.ToTable("PoolStores");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.PoolId).IsRequired();
        builder.Property(x => x.StoreId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ActiveModelId).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.PoolId)
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");
    }
}
