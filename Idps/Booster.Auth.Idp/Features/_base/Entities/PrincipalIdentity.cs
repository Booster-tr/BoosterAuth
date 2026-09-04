using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

public class PrincipalIdentity : Entity
{
    public Guid PrincipalId { get; set; }

    public virtual Principal Principal { get; set; } = default!;

    public Guid IdentityProviderId { get; set; }

    public Guid IsolationId { get; set; }

    public string ExternalSubject { get; set; } = default!;

    public DateTimeOffset? LastLoginAt { get; set; }
}

public class PrincipalIdentityConfiguration : IEntityTypeConfiguration<PrincipalIdentity>
{
    public void Configure(EntityTypeBuilder<PrincipalIdentity> builder)
    {
        builder.ToTable("PrincipalIdentities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.IdentityProviderId).IsRequired();
        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.ExternalSubject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.LastLoginAt);

        builder.HasIndex(x => new { x.IsolationId, x.IdentityProviderId, x.ExternalSubject })
               .IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasIndex(x => x.PrincipalId).IsUnique(false);
    }
}
