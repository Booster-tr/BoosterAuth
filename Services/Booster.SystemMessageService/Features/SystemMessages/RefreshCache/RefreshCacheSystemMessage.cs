using Booster.Database.Repositories;
using Booster.Caching.Abstraction;
using Booster.SystemMessageService.Features._base;
using Booster.SystemMessageService.Features.SystemMessages._base.Entities;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.SystemMessageService.Features.SystemMessages.RefreshCache;

public struct RefreshCacheSystemMessagesEvent() : IEvent;

public class RefreshCacheSystemMessagesEventHandler(IAsyncRepository<SystemMessage, SystemMessageDbContext> repository, ICacheService cacheService) : IEventHandler<RefreshCacheSystemMessagesEvent>
{
    string CacheKey(string language, string key) => $"trans:{language}:{key}";

    /// <summary>
    /// Ceviriler bu sureyle yazilir ve CacheSeederService her 70 dakikada yeniler; sure yenileme araligindan UZUN olmali.
    /// Sure verilmezse onbellegin varsayilani (30 dk) uygulanir ve ceviriler bir sonraki yenilemeye kadar ~40 dk silinmis
    /// kalirdi (ekranlarda "idp.password_too_short" gibi ham anahtarlar gorunurdu).
    /// </summary>
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(3);

    public async Task HandleAsync(RefreshCacheSystemMessagesEvent eventModel, CancellationToken ct)
    {
        var allMessages = await repository.ListAsync(
        index: 0,
        size: int.MaxValue,
        include: x => x.Include(m => m.Translations),
        cancellationToken: ct);

        if (allMessages?.Items == null || !allMessages.Items.Any())
            return;

        var tasks = allMessages.Items
            .SelectMany(m => m.Translations.Select(t =>
            {
                var lang = t.LanguageCode.ToLowerInvariant().Trim();
                var key = m.Key.ToLowerInvariant().Trim();
                var value = $"{t.Title}|{t.Message}";

                var cacheKey = CacheKey(lang, key);

                return cacheService.SetAsync(cacheKey, value, expiry: Expiry);
            }));

        await Task.WhenAll(tasks);
    }

}