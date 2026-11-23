using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Licensing.Portal.Data.Entities;

/// <summary>
/// Lisans imzalama anahtari. Portal ilk acilista kendisi uretir; elle islem yoktur.
/// Private key <c>ISecretProtector</c> (AES-GCM, anahtari cluster Secret'inda) ile SIFRELI saklanir. Boylece yalnizca
/// veritabani yedegini ele geciren biri lisans uretemez.
/// </summary>
public class SigningKey : Entity
{
    public string Kid { get; set; } = default!;

    /// <summary>32 baytlik Ed25519 private key; base64, sonra ISecretProtector ile sifreli.</summary>
    public string ProtectedPrivateKey { get; set; } = default!;

    /// <summary>Urune (LicensePublicKeys) gomulen deger; base64url, gizli degil.</summary>
    public string PublicKey { get; set; } = default!;

    /// <summary>Yeni lisanslar yalnizca aktif anahtarla imzalanir. Ayni anda tek aktif anahtar olur.</summary>
    public bool IsActive { get; set; }
}

internal sealed class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.ToTable("SigningKeys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kid).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Kid).IsUnique();
        builder.Property(x => x.ProtectedPrivateKey).IsRequired();
        builder.Property(x => x.PublicKey).HasMaxLength(64).IsRequired();

        // Iki pod ayni anda anahtar uretmeye calisirsa ikincisi bu indekse takilir ve mevcut anahtari kullanir.
        builder.HasIndex(x => x.IsActive).IsUnique().HasFilter("\"IsActive\" = true");
    }
}
