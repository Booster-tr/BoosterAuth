using Booster.SystemMessageService.Features.SystemMessages.RefreshCache;

namespace Booster.SystemMessageService.Seeders;

public class CacheSeederService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        await RefreshCache(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(70));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshCache(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            
        }
    }

    private async Task RefreshCache(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RefreshCacheSystemMessagesEventHandler>();

        await handler.HandleAsync(new RefreshCacheSystemMessagesEvent(), ct);
    }
}
