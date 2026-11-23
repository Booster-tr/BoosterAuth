using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Licensing.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Common.Internal.Licensing;

public enum LicensedResource
{
    Users = 1,
    Pools = 2,
    Clients = 3,
}

/// <summary>
/// Lisans limitlerinin TEK kontrol noktasi. Kontrol BACKEND'dedir; panel yalnizca gosterir.
/// <para>
/// Yalnizca YENI kayit olusturmayi engeller. Mevcut kullanici/pool/client'lar limit asimindan ASLA etkilenmez
/// (giris, token ve guncelleme yollarinda bu servis cagrilmaz).
/// </para>
/// </summary>
public interface ILicenseLimitService
{
    /// <summary>
    /// Kaynakta yer varsa doner, yoksa <c>LICENSE_LIMIT_*</c> ile reddeder.
    /// <para>
    /// YARIS DURUMU: cagiran acik bir transaction icinde olmalidir. Burada transaction sonuna kadar suren bir
    /// Postgres advisory lock alinir; ayni anda gelen iki istek sirayla sayilir ve son bos yeri yalnizca biri alir.
    /// Cagiran kaydi ekleyip SaveChanges + Commit yaptiginda kilit kalkar.
    /// </para>
    /// </summary>
    /// <param name="db">Kaydin eklenecegi DbContext (kilit ayni baglantida alinir).</param>
    /// <param name="countExisting">Sayilan mevcut kayitlar (yonetim havuzu ve silinmisler HARIC).</param>
    /// <param name="rejectWith">Doluysa firlatilacak mesaj anahtari; verilmezse kaynaga gore LICENSE_LIMIT_*.</param>
    Task EnsureCanCreateAsync(DbContext db, LicensedResource resource, Func<CancellationToken, Task<int>> countExisting,
        CancellationToken ct, string? rejectWith = null);

    /// <summary>Gosterim icin uygulanan limit; durum bilinemiyorsa null.</summary>
    ValueTask<int?> GetLimitAsync(LicensedResource resource, CancellationToken ct);
}

public sealed class LicenseLimitService(ILicenseStatusProvider license) : ILicenseLimitService
{
    // Kaynak basina sabit kilit anahtari (bu uygulamaya ozel bir aralik).
    private const long LockBase = 0x426F6F73_7400; // "Boost"

    public async Task EnsureCanCreateAsync(DbContext db, LicensedResource resource,
        Func<CancellationToken, Task<int>> countExisting, CancellationToken ct, string? rejectWith = null)
    {
        var limit = await GetLimitAsync(resource, ct);

        // Durum bilinemiyor (Management'a ulasilamadi): engelleme yok. Altyapi arizasi kayitlari durdurmasin.
        if (limit is null)
            return;

        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Lisans limit kontrolu acik bir transaction icinde yapilmalidir (yaris durumu).");

        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({LockBase + (int)resource})", ct);

        if (await countExisting(ct) >= limit.Value)
            throw new BusinessException(rejectWith ?? resource switch
            {
                LicensedResource.Users => LicenseMessageKeys.LimitUsers,
                LicensedResource.Pools => LicenseMessageKeys.LimitPools,
                _ => LicenseMessageKeys.LimitClients,
            });
    }

    public async ValueTask<int?> GetLimitAsync(LicensedResource resource, CancellationToken ct)
    {
        var limits = (await license.GetAsync(ct)).Limits;
        return limits is null
            ? null
            : resource switch
            {
                LicensedResource.Users => limits.Users,
                LicensedResource.Pools => limits.Pools,
                _ => limits.Clients,
            };
    }
}
