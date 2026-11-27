using Booster.Auth.Common.Internal.Notifications;
using Booster.NotificationService.Features._base.Entities;
using Booster.NotificationService.Infrastructures.Senders._base;
using FluentEmail.Core;
using FluentEmail.Smtp;
using System.Net;
using System.Net.Mail;

namespace Booster.NotificationService.Infrastructures.Senders;

public class SmtpEmailSender(MailSettings settings) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<SendResult> SendAsync(Notification notification, CancellationToken ct = default)
    {
        try
        {
            var sender = new SmtpSender(() => new SmtpClient(settings.SmtpHost)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(settings.UserName, settings.Password),
                EnableSsl = settings.EnableSsl,
                Port = settings.Port
            });

            Email.DefaultSender = sender;

            var email = await Email
                .From(settings.Sender, settings.DisplayName)
                .To(notification.Target)
                .Subject(notification.Subject ?? string.Empty)
                .Body(notification.Body, isHtml: true)
                .SendAsync(ct);

            return email.Successful
                ? SendResult.Ok()
                : SendResult.Fail(string.Join(", ", email.ErrorMessages));
        }
        catch (Exception ex)
        {
            return SendResult.Fail(ex.Message);
        }
    }
}
