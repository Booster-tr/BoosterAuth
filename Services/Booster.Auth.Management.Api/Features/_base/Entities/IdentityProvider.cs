using Booster.Auth.Database.Models.Enums;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Booster.Auth.Management.Api.Features._base.Entities;


public class IdentityProvider : Entity
{
    public Guid UserPoolId { get; set; }

    public virtual UserPool UserPool { get; set; } = default!;

    public string Key { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string ProviderType { get; set; } = default!;

    public EntityStatus Status { get; set; } = EntityStatus.Active;

    public Dictionary<string, string> Settings { get; set; } = [];

    public string? ClientSecretEncrypted { get; set; }
}

public class IdentityProviderConfiguration : IEntityTypeConfiguration<IdentityProvider>
{

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

    public void Configure(EntityTypeBuilder<IdentityProvider> builder)
    {
        builder.ToTable("IdentityProviders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.UserPoolId).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ProviderType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ClientSecretEncrypted).HasMaxLength(2000);

        builder.Property(x => x.Settings)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonOptions),
                   v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions) ?? new Dictionary<string, string>())

               .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                   (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                   d => d.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
                   d => new Dictionary<string, string>(d)));

        builder.HasIndex(x => new { x.UserPoolId, x.Key })
               .IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasOne(x => x.UserPool)
               .WithMany(x => x.IdentityProviders)
               .HasForeignKey(x => x.UserPoolId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
