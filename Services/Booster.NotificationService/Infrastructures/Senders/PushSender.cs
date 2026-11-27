using Booster.Auth.Common.Internal.Notifications;
using Booster.NotificationService.Features._base.Entities;

namespace Booster.NotificationService.Infrastructures.Senders;

public class PushSender : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Push;

    public Task<SendResult> SendAsync(Notification notification, CancellationToken ct = default)
        => Task.FromResult(SendResult.Fail("push_provider_not_configured"));
}
