using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.NotificationService.Features._base.Entities;

namespace Booster.NotificationService.Infrastructures.Senders;

public class SmsSender : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public Task<SendResult> SendAsync(Notification notification, CancellationToken ct = default)
        => Task.FromResult(SendResult.Ok(/*"sms_provider_not_configured"*/));
}
