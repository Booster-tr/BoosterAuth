using Booster.Auth.Idp.Features._base.Enums;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

public class PrincipalLink : Entity
{
    public Guid PrincipalIdA { get; set; }
    public Guid PrincipalIdB { get; set; }

    public PrincipalLinkType LinkType { get; set; }

    public Guid LinkedByPrincipalId { get; set; }
}

public class PrincipalLinkConfiguration : IEntityTypeConfiguration<PrincipalLink>
{
    public void Configure(EntityTypeBuilder<PrincipalLink> builder)
    {
        builder.ToTable("PrincipalLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.PrincipalIdA).IsRequired();
        builder.Property(x => x.PrincipalIdB).IsRequired();
        builder.Property(x => x.LinkType).HasConversion<int>();
        builder.Property(x => x.LinkedByPrincipalId).IsRequired();

        builder.HasIndex(x => new { x.PrincipalIdA, x.PrincipalIdB }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasIndex(x => x.PrincipalIdA).IsUnique(false);
        builder.HasIndex(x => x.PrincipalIdB).IsUnique(false);
    }
}
