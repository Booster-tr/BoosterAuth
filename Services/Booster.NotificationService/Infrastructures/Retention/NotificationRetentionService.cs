using Booster.NotificationService.Features._base;
using Microsoft.EntityFrameworkCore;

namespace Booster.NotificationService.Infrastructures.Retention;

/// <summary>
/// Gonderim kayitlarini saklama suresi dolunca kalici olarak siler (KVKK: amacla sinirli saklama). Kayitlar yalnizca
/// gonderim sorunlarini incelemek icindir; varsayilan 30 gun (<c>NotificationRetentionDays</c>). Acilista ve gunde bir.
/// </summary>
public sealed class NotificationRetentionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NotificationRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var days = configuration.GetValue("NotificationRetentionDays", 30);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
                var removed = await db.Notifications.IgnoreQueryFilters()
                    .Where(n => n.CreatedTime < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (removed > 0)
                    logger.LogInformation("{Count} bildirim kaydi saklama suresi ({Days} gun) doldugu icin silindi.", removed, days);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Bildirim kayitlari temizlenemedi.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
