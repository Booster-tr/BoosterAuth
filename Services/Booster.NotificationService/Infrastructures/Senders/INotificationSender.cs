using Booster.Auth.Common.Internal.Notifications;
using Booster.NotificationService.Features._base.Entities;

namespace Booster.NotificationService.Infrastructures.Senders;

public readonly record struct SendResult(bool IsSuccess, string? FailureMessage)
{
    public static SendResult Ok() => new(true, null);

    public static SendResult Fail(string message) => new(false, message);
}

public interface INotificationSender
{
    NotificationChannel Channel { get; }

    Task<SendResult> SendAsync(Notification notification, CancellationToken ct = default);
}
