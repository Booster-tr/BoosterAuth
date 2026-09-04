using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Booster.Auth.Idp.Features._base.Entities;

public class LocalCredential : Entity
{
    public Guid PrincipalId { get; set; }

    [JsonIgnore] public virtual Principal Principal { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public DateTimeOffset? PasswordChangedAt { get; set; }

    public bool MustChangePassword { get; set; }

    public int FailedAttemptCount { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }
}

public class LocalCredentialConfiguration : IEntityTypeConfiguration<LocalCredential>
{
    public void Configure(EntityTypeBuilder<LocalCredential> builder)
    {
        builder.ToTable("LocalCredentials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PasswordChangedAt);
        builder.Property(x => x.MustChangePassword).HasDefaultValue(false);
        builder.Property(x => x.FailedAttemptCount).HasDefaultValue(0);
        builder.Property(x => x.LockedUntil);

        builder.HasIndex(x => x.PrincipalId).IsUnique();
    }
}
