using Booster.Database.Repositories;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.NotificationService.Features._base;
using Booster.NotificationService.Features._base.Entities;
using Booster.NotificationService.Features._base.Entities.Enums;
using Booster.NotificationService.Infrastructures.Senders;
using Booster.NotificationService.Infrastructures.Templating;
using MassTransit;

namespace Booster.NotificationService.Features._amqp.Consumers.NotificationRequested;

public class NotificationRequestedEventConsumer(
    IAsyncRepository<Notification, NotificationDbContext> notificationRepository,
    ITemplateRenderer templateRenderer,
    IEnumerable<INotificationSender> senders)
    : IConsumer<NotificationRequestedEvent>
{
    public async Task Consume(ConsumeContext<NotificationRequestedEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        var rendered = await templateRenderer.RenderAsync(
            message.TemplateKey, message.Channel, message.UserPoolId, message.LanguageCode, message.Data, ct);

        if (rendered is null)
            return;

        var notification = new Notification
        {
            TenantId = message.TenantId,
            UserPoolId = message.UserPoolId,
            PrincipalId = message.PrincipalId,
            TemplateKey = message.TemplateKey,
            Channel = message.Channel,
            Target = message.Target,
            LanguageCode = rendered.Value.LanguageCode,
            Subject = rendered.Value.Subject,
            Body = rendered.Value.Body,
            Status = NotificationStatus.Pending
        };
        notification.SetId();

        var sender = senders.FirstOrDefault(s => s.Channel == message.Channel);
        if (sender is null)
        {
            notification.Status = NotificationStatus.Failed;
            notification.FailureMessage = "channel_sender_not_registered";
        }
        else
        {
            notification.AttemptCount = 1;

            var result = await sender.SendAsync(notification, ct);

            notification.Status = result.IsSuccess ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SentTime = result.IsSuccess ? DateTimeOffset.UtcNow : null;
            notification.FailureMessage = result.FailureMessage;
        }

        await notificationRepository.AddAsync(notification, ct);
        await notificationRepository.SaveChangesAsync(ct);
    }
}
