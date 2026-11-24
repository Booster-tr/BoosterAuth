using System.Globalization;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Hashing.TwoWays.Aes;
using Booster.Licensing.Clock;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Licensing;

/// <summary>
/// Kurulum satirinin tek erisim noktasi; saat geri alma korumasinin saklama yeri de burasidir.
/// En ileri zaman sifreli ve kurulum GUID'ine bagli tutulur (<see cref="Installation.ClockMarkProtected"/>): elle
/// geri alinamaz, baska kurulumdan kopyalanamaz; silinirse <see cref="LicenseClockTamperedException"/>.
/// </summary>
public sealed class InstallationStore(AuthNDbContext db, ISecretProtector protector) : ILicenseClockStore
{
    /// <summary>Satir yoksa olusturur (ilk acilis). Iki pod ayni anda baslarsa biri PK'ya takilir ve mevcut satiri okur.</summary>
    public async Task<Installation> EnsureAsync(CancellationToken ct)
    {
        var row = await db.Set<Installation>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == Installation.RowId, ct);
        if (row is not null) return row;

        var installationId = Guid.NewGuid();
        row = new Installation
        {
            Id = Installation.RowId,
            InstallationId = installationId,
            // Saat kaydi satirla BIRLIKTE yazilir: sonradan bos bulunursa silinmis demektir.
            ClockMarkProtected = Protect(installationId, DateTimeOffset.UtcNow)
        };
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

    /// <exception cref="LicenseClockTamperedException">Kayit bos, cozulemiyor ya da baska kuruluma ait.</exception>
    public async Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken ct = default)
    {
        var row = await GetAsync(ct);

        if (row.ClockMarkProtected is null)
        {
            // Onceki surumden gecis: sifresiz deger bir kez tasinir ve bosaltilir. Ikisi de bossa kayit silinmistir.
            if (row.ClockHighWaterMark is not { } legacy)
                throw new LicenseClockTamperedException("missing");

            var migrated = Protect(row.InstallationId, legacy);
            await db.Set<Installation>().Where(i => i.Id == Installation.RowId && i.ClockMarkProtected == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.ClockMarkProtected, migrated)
                    .SetProperty(i => i.ClockHighWaterMark, (DateTimeOffset?)null), ct);
            return legacy;
        }

        return Unprotect(row.InstallationId, row.ClockMarkProtected);
    }

    /// <summary>
    /// Yalnizca ILERI tasir. Sifreli deger SQL'de karsilastirilamadigi icin okunan degere gore kosullu yazilir
    /// (compare-and-swap): ayni anda yazan baska pod varsa bu yazim atlanir, onun degeri kalir.
    /// </summary>
    public async Task AdvanceHighWaterMarkAsync(DateTimeOffset value, CancellationToken ct = default)
    {
        var row = await GetAsync(ct);
        if (row.ClockMarkProtected is null)
            return;

        var current = Unprotect(row.InstallationId, row.ClockMarkProtected);
        if (current >= value)
            return;

        var previous = row.ClockMarkProtected;
        var next = Protect(row.InstallationId, value);
        await db.Set<Installation>().Where(i => i.Id == Installation.RowId && i.ClockMarkProtected == previous)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ClockMarkProtected, next), ct);
    }

    private string Protect(Guid installationId, DateTimeOffset value) =>
        protector.Protect($"{installationId:N}|{value.UtcTicks.ToString(CultureInfo.InvariantCulture)}");

    private DateTimeOffset Unprotect(Guid installationId, string protectedValue)
    {
        var plain = protector.Unprotect(protectedValue)
            ?? throw new LicenseClockTamperedException("undecryptable");

        var parts = plain.Split('|');
        if (parts.Length != 2 || parts[0] != installationId.ToString("N")
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks <= 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new LicenseClockTamperedException("mismatch");

        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
