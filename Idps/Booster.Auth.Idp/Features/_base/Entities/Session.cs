using Booster.Auth.Idp.Features._base.Enums;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Booster.Auth.Idp.Features._base.Entities;

public class Session : IsolatedEntity
{
    public Guid PrincipalId { get; set; }

    [JsonIgnore] public virtual Principal Principal { get; set; } = default!;

    public Guid ClientId { get; set; }

    public string TokenHash { get; set; } = default!;

    public SessionStatus Status { get; set; } = SessionStatus.Active;

    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? AuthMethod { get; set; }

    [JsonIgnore]
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = [];

}

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(SessionStatus.Active);
        builder.Property(x => x.DeviceName).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.UserAgent).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.IpAddress).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.Location).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.LastActivityAt);
        builder.Property(x => x.ExpiresAt);
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.AuthMethod).HasMaxLength(100).IsRequired(false);

        builder.HasIndex(x => x.TokenHash).IsUnique().HasFilter("\"DeletedTime\" IS NULL");

        builder.HasIndex(x => x.PrincipalId).IsUnique(false);
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.Principal)
               .WithMany()
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

    }
}