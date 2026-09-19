using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class ClientAllowedScope : Entity
{
    public Guid ClientId { get; set; }

    public virtual Client Client { get; set; } = default!;

    public string Scope { get; set; } = default!;
}

public class ClientAllowedScopeConfiguration : IEntityTypeConfiguration<ClientAllowedScope>
{
    public void Configure(EntityTypeBuilder<ClientAllowedScope> builder)
    {
        builder.ToTable("ClientAllowedScopes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(200).IsRequired();



        builder.HasIndex(x => new { x.ClientId, x.Scope })
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");
    }
}