using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Licensing.Portal.Data.Entities;

/// <summary>
/// Uretilmis her lisans. Id, token'daki licenseId ile aynidir. Token da saklanir; boylece musteri kaybettiginde
/// ayni token yeniden gosterilebilir. Token gizli degildir: yalnizca kendi kurulum GUID'inde calisir.
/// </summary>
public class IssuedLicense : Entity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid InstallationId { get; set; }

    public string Plan { get; set; } = default!;

    /// <summary>trial veya paid.</summary>
    public string Type { get; set; } = default!;

    public int MaxUsers { get; set; }
    public int MaxPools { get; set; }
    public int MaxClients { get; set; }

    public List<string> AllowedDomains { get; set; } = [];

    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int GraceDays { get; set; }

    public string Kid { get; set; } = default!;

    public string Token { get; set; } = default!;

    /// <summary>Lisansi ureten kisinin e-postasi (admin) ya da "self-service" (Faz 2.2).</summary>
    public string IssuedBy { get; set; } = default!;

    public string? Note { get; set; }
}

internal sealed class IssuedLicenseConfiguration : IEntityTypeConfiguration<IssuedLicense>
{
    public void Configure(EntityTypeBuilder<IssuedLicense> builder)
    {
        builder.ToTable("IssuedLicenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Plan).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Kid).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Token).IsRequired();
        builder.Property(x => x.IssuedBy).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => x.InstallationId);
        builder.HasOne(x => x.Customer).WithMany(c => c.Licenses).HasForeignKey(x => x.CustomerId);
    }
}
