using Booster.Database.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Booster.Auth.Idp.Features._base.Entities;

public class RefreshToken : IsolatedEntity
{
    public Guid PrincipalId { get; set; }
    [JsonIgnore] public virtual Principal Principal { get; set; } = default!;

    public Guid SessionId { get; set; }
    [JsonIgnore] public virtual Session Session { get; set; } = default!;

    public Guid ClientId { get; set; }

    public string TokenHash { get; set; } = default!;

    public string GrantedScopes { get; set; } = default!;

    public string GrantedRoles { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedByTokenId { get; set; }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.IsolationId).IsRequired();
        builder.Property(x => x.PrincipalId).IsRequired();
        builder.Property(x => x.SessionId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GrantedScopes).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.GrantedRoles).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ExpiresAt);
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.ReplacedByTokenId);

        // TokenHash ile hızlı arama (refresh doğrulaması).
        builder.HasIndex(x => x.TokenHash).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.Principal)
               .WithMany()
               .HasForeignKey(x => x.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Session)
               .WithMany(s => s.RefreshTokens)
               .HasForeignKey(x => x.SessionId)
               .OnDelete(DeleteBehavior.Cascade);

        // ClientId dış (yönetim) — FK YOK.
    }
}