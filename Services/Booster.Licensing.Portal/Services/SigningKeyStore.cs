using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Hashing.TwoWays.Aes;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Signing;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Imzalama anahtarlarinin tek giris noktasi. Private key yalnizca bu sinifta cozulur ve hicbir yere loglanmaz.
/// </summary>
public sealed class SigningKeyStore(LicensingDbContext db, ISecretProtector protector, ILogger<SigningKeyStore> logger)
{
    /// <summary>
    /// Aktif anahtar yoksa uretir (ilk acilis). Kid bicimi: k&lt;yil&gt;&lt;harf&gt;, or. k2026a.
    /// Yalnizca kid ve public key loglanir.
    /// </summary>
    public async Task EnsureActiveKeyAsync(CancellationToken ct)
    {
        if (await db.SigningKeys.AnyAsync(k => k.IsActive, ct))
            return;

        var existing = await db.SigningKeys.Select(k => k.Kid).ToListAsync(ct);
        var year = DateTime.UtcNow.Year;
        var kid = Enumerable.Range('a', 26).Select(c => $"k{year}{(char)c}").First(k => !existing.Contains(k));

        var key = LicenseSigningKey.Generate(kid);
        db.SigningKeys.Add(new SigningKey
        {
            Id = EntityId.New(),
            Kid = kid,
            ProtectedPrivateKey = protector.Protect(Convert.ToBase64String(key.ExportPrivateKey())),
            PublicKey = key.PublicKeyBase64Url,
            IsActive = true,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Yeni lisans imzalama anahtari uretildi: kid={Kid} public={PublicKey}. Public key urune (LicensePublicKeys) eklenmeli.",
                kid, key.PublicKeyBase64Url);
        }
        catch (DbUpdateException)
        {
            // Baska bir pod ayni anda uretti (tekil aktif anahtar indeksi). Onun anahtari kullanilir.
            db.ChangeTracker.Clear();
        }
    }

    public async Task<LicenseSigningKey> GetActiveAsync(CancellationToken ct)
    {
        var row = await db.SigningKeys.AsNoTracking().SingleAsync(k => k.IsActive, ct);
        var plain = protector.Unprotect(row.ProtectedPrivateKey)
                    ?? throw new InvalidOperationException($"Imzalama anahtari ({row.Kid}) cozulemedi; sifreleme anahtari degismis olabilir.");
        return LicenseSigningKey.FromPrivateKey(row.Kid, Convert.FromBase64String(plain));
    }

    /// <summary>Portalin kendi urettigi token'lari dogrulamak icin tum public key'ler (kid -> 32 bayt).</summary>
    public async Task<IReadOnlyDictionary<string, byte[]>> GetPublicKeysAsync(CancellationToken ct)
    {
        var rows = await db.SigningKeys.AsNoTracking().Select(k => new { k.Kid, k.PublicKey }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Kid, r => System.Buffers.Text.Base64Url.DecodeFromChars(r.PublicKey), StringComparer.Ordinal);
    }

    public Task<List<SigningKey>> ListAsync(CancellationToken ct) =>
        db.SigningKeys.AsNoTracking().OrderByDescending(k => k.CreatedTime).ToListAsync(ct);
}
