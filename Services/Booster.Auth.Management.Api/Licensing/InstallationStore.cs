using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Licensing.Clock;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Licensing;

/// <summary>Kurulum satirinin tek erisim noktasi; saat geri alma korumasinin saklama yeri de burasidir.</summary>
public sealed class InstallationStore(AuthNDbContext db) : ILicenseClockStore
{
    /// <summary>Satir yoksa olusturur (ilk acilis). Iki pod ayni anda baslarsa biri PK'ya takilir ve mevcut satiri okur.</summary>
    public async Task<Installation> EnsureAsync(CancellationToken ct)
    {
        var row = await db.Set<Installation>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == Installation.RowId, ct);
        if (row is not null) return row;

        row = new Installation { Id = Installation.RowId, InstallationId = Guid.NewGuid() };
        db.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.Set<Installation>().AsNoTracking().SingleAsync(i => i.Id == Installation.RowId, ct);
        }
    }

    public Task<Installation> GetAsync(CancellationToken ct) =>
        db.Set<Installation>().AsNoTracking().SingleAsync(i => i.Id == Installation.RowId, ct);

    public async Task SaveLicenseAsync(string token, string activatedBy, DateTimeOffset at, CancellationToken ct)
    {
        await db.Set<Installation>().Where(i => i.Id == Installation.RowId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.LicenseToken, token)
                .SetProperty(i => i.LicenseActivatedAt, at)
                .SetProperty(i => i.LicenseActivatedBy, activatedBy)
                .SetProperty(i => i.UpdatedTime, at), ct);
    }

    public async Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken ct = default) =>
        await db.Set<Installation>().AsNoTracking().Where(i => i.Id == Installation.RowId)
            .Select(i => i.ClockHighWaterMark).SingleOrDefaultAsync(ct);

    /// <summary>
    /// Yalnizca ILERI tasir (WHERE ... &lt; yeni deger). Ayni anda calisan iki pod birbirinin degerini geri alamaz;
    /// sistem saati geri alinmis bir pod da kaydi geri goturemez.
    /// </summary>
    public async Task AdvanceHighWaterMarkAsync(DateTimeOffset value, CancellationToken ct = default)
    {
        await db.Set<Installation>()
            .Where(i => i.Id == Installation.RowId && (i.ClockHighWaterMark == null || i.ClockHighWaterMark < value))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ClockHighWaterMark, value), ct);
    }
}
