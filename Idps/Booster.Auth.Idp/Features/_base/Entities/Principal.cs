using Booster.Auth.Idp.Features._base.Enums;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Booster.Auth.Idp.Features._base.Entities;

public class Principal : IsolatedEntity
{
    public PrincipalType Type { get; set; } = PrincipalType.User;

    public PrincipalStatus Status { get; set; } = PrincipalStatus.Active;

    public string DisplayName { get; set; } = default!;

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public bool EmailVerified { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneVerified { get; set; }

    [JsonIgnore] public virtual ICollection<PrincipalIdentity> Identities { get; set; } = [];
    [JsonIgnore] public virtual ServiceAccount? ServiceAccount { get; set; }
    [JsonIgnore] public virtual LocalCredential? LocalCredential { get; set; }
}

public class PrincipalConfiguration : IEntityTypeConfiguration<Principal>
{
    public void Configure(EntityTypeBuilder<Principal> builder)
    {
        builder.ToTable("Principals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.IsolationId).IsRequired();

        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(PrincipalStatus.Active);
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired(false);
        builder.Property(x => x.EmailVerified).HasDefaultValue(false);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.PhoneVerified).HasDefaultValue(false);

        builder.HasIndex(x => x.IsolationId).IsUnique(false);

        builder.HasIndex(x => new { x.IsolationId, x.UserName }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.HasIndex(x => new { x.IsolationId, x.Email }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.HasIndex(x => new { x.IsolationId, x.PhoneNumber }).IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasMany(x => x.Identities)
               .WithOne(x => x.Principal)
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LocalCredential)
               .WithOne(x => x.Principal)
               .HasForeignKey<LocalCredential>(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceAccount)
               .WithOne(x => x.Principal)
               .HasForeignKey<ServiceAccount>(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}