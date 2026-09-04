using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

public class ServiceAccount : Entity
{
    public Guid PrincipalId { get; set; }

    public virtual Principal Principal { get; set; } = default!;

    public Guid IsolationId { get; set; }

    public string Key { get; set; } = default!;

    public string SecretHash { get; set; } = default!;

    public string Name { get; set; } = default!;

    public DateTimeOffset? ExpiresAt { get; set; }
}

public class ServiceAccountConfiguration : IEntityTypeConfiguration<ServiceAccount>
{
    public void Configure(EntityTypeBuilder<ServiceAccount> builder)
    {
        builder.ToTable("ServiceAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SecretHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ExpiresAt);

        builder.HasIndex(x => new { x.IsolationId, x.Key }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasIndex(x => x.PrincipalId).IsUnique();
    }
}
