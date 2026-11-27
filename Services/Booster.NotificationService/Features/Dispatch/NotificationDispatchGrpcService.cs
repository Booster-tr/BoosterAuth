using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Common.Internal.Notifications;
using Booster.Auth.Notification.Grpc;
using Booster.Database.Repositories;
using Booster.Hashing.TwoWays.Aes;
using Booster.NotificationService.Features._base;
using Booster.NotificationService.Features._base.Entities.Enums;
using Booster.NotificationService.Infrastructures.Senders;
using Booster.NotificationService.Infrastructures.Templating;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NotificationEntity = Booster.NotificationService.Features._base.Entities.Notification;

namespace Booster.NotificationService.Features.Dispatch;

/// <summary>
/// IdP ve lisans portalinin bildirim istekleri (gRPC, yalnizca ic ag: 8081). Cagiranlar yaniti beklemez; gonderim
/// burada istek icinde yapilir. Yeniden deneme yoktur: basarisiz gonderim kayitta Failed olarak kalir.
/// </summary>
[AllowAnonymous]
public sealed class NotificationDispatchGrpcService(
    IAsyncRepository<NotificationEntity, NotificationDbContext> notificationRepository,
    NotificationDbContext db,
    ITemplateRenderer templateRenderer,
    IEnumerable<INotificationSender> senders,
    ISecretProtector protector)
    : NotificationDispatchService.NotificationDispatchServiceBase
{
    public override async Task<NotificationAck> Send(SendNotificationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var channel = (NotificationChannel)request.Channel;

        if (!Enum.IsDefined(channel) || string.IsNullOrWhiteSpace(request.TemplateKey) || string.IsNullOrWhiteSpace(request.Target))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bildirim istegi gecersiz."));

        var userPoolId = ParseGuid(request.UserPoolId);
        var data = new Dictionary<string, string>(request.Data);

        var rendered = await templateRenderer.RenderAsync(
            request.TemplateKey, channel, userPoolId, request.LanguageCode, data, ct);

        if (rendered is null)
            return new NotificationAck();

        var notification = new NotificationEntity
        {
            TenantId = ParseGuid(request.TenantId),
            UserPoolId = userPoolId,
            PrincipalId = Guid.TryParse(request.PrincipalId, out var principalId) ? principalId : null,
            TemplateKey = request.TemplateKey,
            Channel = channel,
            Target = request.Target,
            LanguageCode = rendered.Value.LanguageCode,
            Subject = rendered.Value.Subject,
            Body = rendered.Value.Body,
            Status = NotificationStatus.Pending
        };
        notification.SetId();

        var sender = senders.FirstOrDefault(s => s.Channel == channel);
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

        // Gonderildikten sonra kayitta alici ve icerik SIFRELI kalir (KVKK m.12): veritabanina erisen biri kodlari, davet
        // baglantilarini ve adresleri okuyamaz. Kayit 30 gun sonra silinir (NotificationRetentionService).
        // Sifreli metin duzden uzundur; sutuna sigmayan (cok nadir) icerik saklanmaz: kayit hatasi gonderilmis bir
        // bildirimi hata gibi gosterirdi.
        var target = protector.Protect(notification.Target);
        var body = protector.Protect(notification.Body);
        notification.Target = target.Length <= 500 ? target : "-";
        notification.Body = body.Length <= 20000 ? body : "-";

        await notificationRepository.AddAsync(notification, ct);
        await notificationRepository.SaveChangesAsync(ct);

        return new NotificationAck();
    }

    /// <summary>
    /// Silinen hesabin gonderim kayitlari (alici adresi, ad, kod iceren govde) kalici olarak silinir: kisisel veri hesap
    /// silindikten sonra burada kalmamali. Kayitlar baska bir is icin gerekmez (yeniden gonderim yok).
    /// </summary>
    public override async Task<NotificationAck> DeletePrincipalNotifications(
        DeletePrincipalNotificationsRequest request, ServerCallContext context)
    {
        var userPoolId = ParseGuid(request.UserPoolId);
        var principalId = ParseGuid(request.PrincipalId);

        await db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.PrincipalId == principalId && n.UserPoolId == userPoolId)
            .ExecuteDeleteAsync(context.CancellationToken);

        return new NotificationAck();
    }

    private static Guid ParseGuid(string value)
        => Guid.TryParse(value, out var id)
            ? id
            : throw new RpcException(new Status(StatusCode.InvalidArgument, "Kimlik gecersiz."));
}
