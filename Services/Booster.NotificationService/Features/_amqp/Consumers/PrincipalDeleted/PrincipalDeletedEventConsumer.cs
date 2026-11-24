using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.NotificationService.Features._base;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Booster.NotificationService.Features._amqp.Consumers.PrincipalDeleted;

/// <summary>
/// Silinen hesabin gonderim kayitlari (alici adresi, ad, kod iceren govde) kalici olarak silinir: kisisel veri hesap
/// silindikten sonra burada kalmamali. Kayitlar baska bir is icin gerekmez (yeniden gonderim yok).
/// </summary>
public class PrincipalDeletedEventConsumer(NotificationDbContext db) : IConsumer<PrincipalDeletedEvent>
{
    public async Task Consume(ConsumeContext<PrincipalDeletedEvent> context)
    {
        var message = context.Message;
        await db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.PrincipalId == message.PrincipalId && n.UserPoolId == message.UserPoolId)
            .ExecuteDeleteAsync(context.CancellationToken);
    }
}
