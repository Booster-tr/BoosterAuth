using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

/// <summary>
/// Kurulumun tek satirlik kaydi: kurulum GUID'i, aktif lisans ve saat geri alma korumasinin en ileri zamani.
/// Satirin Id'si sabittir (<see cref="RowId"/>); kurulum basina tek satir olur.
/// </summary>
public class Installation : Entity
{
    public static readonly Guid RowId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    /// <summary>
    /// Ilk acilista rastgele uretilir ve kurulum yasadigi surece DEGISMEZ. Lisans bu GUID'e baglidir.
    /// Dosyaya yazilmaz; yalnizca veritabanindadir (yedekten donulunce ayni kalir).
    /// </summary>
    public Guid InstallationId { get; set; }

    /// <summary>Panelden yapistirilan ve dogrulamadan GECMIS token. Gecersiz token buraya ASLA yazilmaz.</summary>
    public string? LicenseToken { get; set; }

    public DateTimeOffset? LicenseActivatedAt { get; set; }

    public string? LicenseActivatedBy { get; set; }

    /// <summary>Gorulen en ileri zaman. Yalnizca ileri gider (bkz. LicenseClock).</summary>
    public DateTimeOffset? ClockHighWaterMark { get; set; }
}

public class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
    public void Configure(EntityTypeBuilder<Installation> builder)
    {
        builder.ToTable("Installations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.InstallationId).IsRequired();
        builder.HasIndex(x => x.InstallationId).IsUnique();
        builder.Property(x => x.LicenseActivatedBy).HasMaxLength(320);
    }
}
