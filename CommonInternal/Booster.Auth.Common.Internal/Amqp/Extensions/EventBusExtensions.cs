using Booster.Amqp.Bus;
using Microsoft.Extensions.Logging;

namespace Booster.Auth.Common.Internal.Amqp.Extensions;

public static class EventBusExtensions
{
    /// <summary>
    /// Veritabanı kaydından SONRA gönderilen olaylar için: Kafka hatası isteği bozmaz, yalnızca loglanır
    /// (rider üreticileri outbox kullanmaz; kayıt zaten yapılmıştır).
    /// </summary>
    public static async Task PublishOrLogAsync<T>(this IEventBus eventBus, T message, ILogger logger, CancellationToken ct)
        where T : class
    {
        try
        {
            await eventBus.PublishAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Olayin icerigi YAZILMAZ: bildirim olaylari alici adresi ve tek kullanimlik kod tasir (KVKK).
            logger.LogError(ex, "{EventType} Kafka'ya gönderilemedi.", typeof(T).Name);
        }
    }
}
