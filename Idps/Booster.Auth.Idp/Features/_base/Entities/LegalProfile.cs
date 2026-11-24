using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Idp.Features._base.Entities;

/// <summary>
/// Yasal metinlerdeki veri sorumlusu (uygulama sahibi kurum) bilgileri. Tek satir (<see cref="RowId"/>).
/// Metinlerdeki yer tutucular bunlarla doldurulur; bos alan "[...]" isaretiyle gosterilir ki eksik oldugu gorulsun.
/// </summary>
public class LegalProfile : Entity
{
    public static readonly Guid RowId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    public string? OrganizationName { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? KepAddress { get; set; }
    public string? Website { get; set; }

    /// <summary>Ticaret sicil / MERSIS / vergi gibi tanitici bilgiler (serbest metin).</summary>
    public string? RegistryInfo { get; set; }
}

public class LegalProfileConfiguration : IEntityTypeConfiguration<LegalProfile>
{
    public void Configure(EntityTypeBuilder<LegalProfile> builder)
    {
        builder.ToTable("LegalProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OrganizationName).HasMaxLength(300);
        builder.Property(x => x.Address).HasMaxLength(1000);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.KepAddress).HasMaxLength(320);
        builder.Property(x => x.Website).HasMaxLength(500);
        builder.Property(x => x.RegistryInfo).HasMaxLength(500);
    }
}
