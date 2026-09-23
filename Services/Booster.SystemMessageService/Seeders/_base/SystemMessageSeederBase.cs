using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Database.Repositories;
using Booster.SystemMessageService.Features._base;
using Booster.SystemMessageService.Features.SystemMessages._base.Entities;
using Booster.SystemMessageService.Features.SystemMessages.RefreshCache;

namespace Booster.SystemMessageService.Seeders._base;

/// <summary>
/// Mesaj katalogunu veritabanına yazan seeder'ların ortak iskeleti.
/// <para>
/// Yalnızca EKSİK anahtarlar eklenir; var olan çeviriler üzerine yazılmaz.
/// Üzerine yazsaydı, operasyonun panelden düzelttiği bir metin her açılışta
/// geri alınırdı.
/// </para>
/// </summary>
public abstract class SystemMessageSeederBase(
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    /// <summary>Tek bir mesajın iki dildeki karşılığı.</summary>
    protected readonly record struct Msg(string Key, string TrTitle, string TrMessage, string EnTitle, string EnMessage);

    protected abstract IReadOnlyList<Msg> Messages { get; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider
                .GetRequiredService<IAsyncRepository<SystemMessage, SystemMessageDbContext>>();

            var added = false;
            foreach (var item in Messages)
            {
                if (await repository.AnyAsync(x => x.Key == item.Key, cancellationToken: ct))
                    continue;

                var message = new SystemMessage { Key = item.Key };
                message.SetId();
                message.Translations =
                [
                    NewTranslation(message.Id, "tr-tr", item.TrTitle, item.TrMessage),
                    NewTranslation(message.Id, "en-us", item.EnTitle, item.EnMessage),
                ];

                await repository.AddAsync(message, ct);
                added = true;
            }

            if (!added)
                return;

            await repository.SaveChangesAsync(ct);

            // Yeni kayıtlar önbelleğe hemen yansısın; aksi hâlde servisler
            // bir sonraki periyodik tazelemeye kadar çevrilmemiş anahtar döner.
            var refreshHandler = scope.ServiceProvider
                .GetRequiredService<RefreshCacheSystemMessagesEventHandler>();
            await refreshHandler.HandleAsync(new RefreshCacheSystemMessagesEvent(), ct);
        }
        catch (Exception ex)
        {
            // Seed başarısızlığı servisi çökertmemeli: mesajlar olmadan da
            // uçlar çalışır, kullanıcı yalnızca ham anahtarı görür.
            logger.LogError(ex, "{Seeder} mesajları seed edilemedi.", GetType().Name);
        }
    }

    private static SystemMessageTranslation NewTranslation(Guid messageId, string lang, string title, string message)
    {
        var translation = new SystemMessageTranslation
        {
            SystemMessageId = messageId,
            LanguageCode = lang,
            Title = title,
            Message = message
        };
        translation.SetId();
        return translation;
    }
}
