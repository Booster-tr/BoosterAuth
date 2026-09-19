using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class ScopeDefinition : Entity
{
    public Guid UserPoolId { get; set; }

    public virtual UserPool UserPool { get; set; } = default!;

    public string Scope { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }
}

public class ScopeDefinitionConfiguration : IEntityTypeConfiguration<ScopeDefinition>
{
    public void Configure(EntityTypeBuilder<ScopeDefinition> builder)
    {
        builder.ToTable("ScopeDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.UserPoolId).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired(false);

        builder.HasIndex(x => new { x.UserPoolId, x.Scope })
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");

        builder.HasOne(x => x.UserPool)
               .WithMany(w => w.ScopeDefinitions)
               .HasForeignKey(x => x.UserPoolId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}