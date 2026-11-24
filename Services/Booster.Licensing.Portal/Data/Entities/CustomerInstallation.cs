using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Licensing.Portal.Data.Entities;

/// <summary>
/// Musterinin kayitli bir Booster Auth kurulumu (GUID'i urun panelinde Sistem &gt; Lisans'ta gorunur).
/// Bir GUID yalnizca BIR musteriye ait olabilir; baskasinin kurulumuna lisans alinamasin diye.
/// </summary>
public class CustomerInstallation : Entity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid InstallationId { get; set; }

    public string Label { get; set; } = default!;

    /// <summary>Bu kurulumun kullanacagi ana domain'ler (lisansa yazilir).</summary>
    public List<string> Domains { get; set; } = [];
}

internal sealed class CustomerInstallationConfiguration : IEntityTypeConfiguration<CustomerInstallation>
{
    public void Configure(EntityTypeBuilder<CustomerInstallation> builder)
    {
        builder.ToTable("CustomerInstallations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.InstallationId).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.HasOne(x => x.Customer).WithMany(c => c.Installations).HasForeignKey(x => x.CustomerId);
    }
}

/// <summary>
/// Her deneme lisansi istegi (verilsin ya da reddedilsin). IP basina gunluk sinir ve kotuye kullanimi
/// izlemek icin saklanir.
/// </summary>
public class TrialRequest : Entity
{
    public Guid CustomerId { get; set; }
    public Guid InstallationId { get; set; }
    public string? IpAddress { get; set; }
    public string Email { get; set; } = default!;
    public List<string> Domains { get; set; } = [];
    public bool Granted { get; set; }
    /// <summary>Red sebebi (mesaj anahtari); verildiyse null.</summary>
    public string? RejectReason { get; set; }
    public Guid? LicenseId { get; set; }

    /// <summary>
    /// Istekte kabul edilen Lisans Sozlesmesi surumu (<c>LegalInfo.TermsVersion</c>) ve B2B beyani. Kabulun zamani,
    /// IP'si ve kullanicisi bu kaydin <c>CreatedTime</c>, <see cref="IpAddress"/> ve <see cref="Email"/> alanlaridir.
    /// Bu alan eklenmeden onceki isteklerde bostur.
    /// </summary>
    public string? TermsVersion { get; set; }
}

internal sealed class TrialRequestConfiguration : IEntityTypeConfiguration<TrialRequest>
{
    public void Configure(EntityTypeBuilder<TrialRequest> builder)
    {
        builder.ToTable("TrialRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.RejectReason).HasMaxLength(64);
        builder.Property(x => x.TermsVersion).HasMaxLength(32);
        builder.HasIndex(x => new { x.IpAddress, x.CreatedTime });
        builder.HasIndex(x => x.CustomerId);
    }
}

/// <summary>Lisansin yeni bir kuruluma (GUID) aktarimi. Lisans basina yilda en fazla 3 kez.</summary>
public class LicenseTransfer : Entity
{
    /// <summary>Aktarim zincirinin ilk lisansi; yillik sinir bu lisans uzerinden sayilir.</summary>
    public Guid OriginLicenseId { get; set; }
    public Guid FromLicenseId { get; set; }
    public Guid ToLicenseId { get; set; }
    public Guid FromInstallationId { get; set; }
    public Guid ToInstallationId { get; set; }
    public string RequestedBy { get; set; } = default!;
}

internal sealed class LicenseTransferConfiguration : IEntityTypeConfiguration<LicenseTransfer>
{
    public void Configure(EntityTypeBuilder<LicenseTransfer> builder)
    {
        builder.ToTable("LicenseTransfers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedBy).HasMaxLength(320).IsRequired();
        builder.HasIndex(x => new { x.OriginLicenseId, x.CreatedTime });
    }
}
