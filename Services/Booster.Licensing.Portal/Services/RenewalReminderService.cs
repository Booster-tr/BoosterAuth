using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Bitise 30 ve 7 gun kala yenileme hatirlatmasi.
/// <para>
/// Her lisans ve tur icin EN FAZLA BIR KEZ gonderilir: (LicenseId, Kind) tekil index.i uzerinde once kayit
/// sahiplenilir, sonra e-posta gonderilir. Servis yeniden baslasa da, birden fazla pod calissa da tekrar gitmez.
/// </para>
/// Musteri ayni kurulum icin daha uzun sureli bir lisans almissa (yenilemisse) hatirlatma GONDERILMEZ.
/// </summary>
public sealed class RenewalReminderService(LicensingDbContext db, PortalNotifier notifier, TimeProvider time, ILogger<RenewalReminderService> logger)
{
    public static readonly int[] Kinds = [30, 7];

    /// <returns>Gonderilen hatirlatma sayisi.</returns>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var horizon = now.AddDays(Kinds.Max());

        var candidates = await db.IssuedLicenses.AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => l.Type == LicenseTypes.Paid && l.TransferredToLicenseId == null
                        && l.ExpiresAt > now && l.ExpiresAt <= horizon)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var license in candidates)
        {
            // Yenilenmis: ayni kurulumda daha gec biten ucretli lisans var.
            var renewed = await db.IssuedLicenses.AnyAsync(l => l.InstallationId == license.InstallationId
                && l.Type == LicenseTypes.Paid && l.TransferredToLicenseId == null && l.ExpiresAt > license.ExpiresAt, ct);
            if (renewed) continue;

            var daysLeft = (int)Math.Ceiling((license.ExpiresAt - now).TotalDays);
            // En yakin esik: 7 gun icindeyse 7, degilse 30. Gec kalinan 30 gunluk hatirlatma 7'nin yerine gecmez.
            var kind = Kinds.Where(k => daysLeft <= k).Min();

            // Once SAHIPLEN, sonra gonder. Kafka olayi geri alinamadigi icin (outbox yok) "gonder + kaydet" sirasi
            // es zamanli calismada e-postayi yineler. ON CONFLICT ile yalnizca satiri yazabilen calisma gonderir.
            var reminderId = EntityId.New();
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "LicenseReminders" ("Id", "LicenseId", "Kind", "CreatedTime")
                VALUES ({reminderId}, {license.Id}, {kind}, {now})
                ON CONFLICT ("LicenseId", "Kind") DO NOTHING
                """, ct);
            if (claimed == 0) continue;

            if (await notifier.RenewalReminderAsync(license.Customer, license, daysLeft, ct))
                sent++;
            else
                // Gonderilemedi: sahiplik birakilir, bir sonraki turda tekrar denenir.
                await db.LicenseReminders.Where(r => r.Id == reminderId).ExecuteDeleteAsync(ct);
        }

        if (sent > 0) logger.LogInformation("{Count} yenileme hatirlatmasi gonderildi.", sent);
        return sent;
    }
}

/// <summary>Acilista ve 6 saatte bir hatirlatmalari calistirir (is idempotent; sik calismasi zarar vermez).</summary>
public sealed class RenewalReminderWorker(IServiceScopeFactory scopes, ILogger<RenewalReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RenewalReminderService>().RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Yenileme hatirlatmalari calistirilamadi; bir sonraki turda tekrar denenecek.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
