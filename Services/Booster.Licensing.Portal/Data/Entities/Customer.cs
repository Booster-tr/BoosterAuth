using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Licensing.Portal.Data.Entities;

/// <summary>Lisans sahibi firma. Faz 2.2'de musteri kendi kaydini portalda olusturur (OwnerPrincipalId).</summary>
public class Customer : Entity
{
    public string Name { get; set; } = default!;

    public string ContactEmail { get; set; } = default!;

    public string? TaxOffice { get; set; }

    public string? TaxNumber { get; set; }

    public string? Address { get; set; }

    /// <summary>Firmayi portalda yoneten Booster Auth kullanicisi (sub). Admin'in elle ekledigi firmada bos olabilir.</summary>
    public Guid? OwnerPrincipalId { get; set; }

    public List<IssuedLicense> Licenses { get; set; } = [];
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.TaxOffice).HasMaxLength(100);
        builder.Property(x => x.TaxNumber).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.HasIndex(x => x.OwnerPrincipalId);
    }
}
