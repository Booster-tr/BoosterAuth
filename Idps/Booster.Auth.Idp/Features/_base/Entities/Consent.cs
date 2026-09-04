using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Booster.Auth.Idp.Features._base.Entities;


public class Consent : Entity, IIsolatedEntity
{
    public Guid IsolationId { get; set; }

    public Guid PrincipalId { get; set; }

    [JsonIgnore] public virtual Principal Principal { get; set; } = default!;

    public Guid ClientId { get; set; }

    public string GrantedScopes { get; set; } = default!;

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}

public class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    public void Configure(EntityTypeBuilder<Consent> builder)
    {
        builder.ToTable("Consents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GrantedScopes).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.ExpiresAt);

        builder.HasIndex(x => new { x.PrincipalId, x.ClientId })
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");

        builder.HasOne(x => x.Principal)
               .WithMany()
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

    }
}