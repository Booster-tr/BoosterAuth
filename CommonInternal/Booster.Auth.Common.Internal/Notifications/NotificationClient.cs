using Booster.Auth.Common.Internal.Helpers;
using Booster.Auth.Notification.Grpc;
using Booster.Common.Extensions.ServiceExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Booster.Auth.Common.Internal.Notifications;

public class NotificationSettings
{
    public string GrpcAddress { get; set; } = default!;

    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Notification servisine gRPC ile bildirim istegi (IdP, lisans portali). Cagri BEKLENMEZ (<see cref="FireAndForget"/>):
/// Notification kapaliysa bildirim kaybolur; is islemi etkilenmez.
/// </summary>
public sealed class NotificationClient(
    NotificationDispatchService.NotificationDispatchServiceClient client,
    ILogger<NotificationClient> logger)
{
    public void Send(NotificationRequest notification)
    {
        // Mesaj cagri anindaki haliyle kopyalanir; arka planda cagiranin nesnesine dokunulmaz.
        var request = new SendNotificationRequest
        {
            TenantId = notification.TenantId.ToString(),
            UserPoolId = notification.UserPoolId.ToString(),
            PrincipalId = notification.PrincipalId?.ToString() ?? string.Empty,
            TemplateKey = notification.TemplateKey,
            Channel = (int)notification.Channel,
            Target = notification.Target,
            LanguageCode = notification.LanguageCode,
            Data = { notification.Data }
        };

        FireAndForget.Run(logger, $"Notification.Send({notification.TemplateKey})",
            ct => client.SendAsync(request, cancellationToken: ct).ResponseAsync);
    }

    public void DeletePrincipalNotifications(Guid userPoolId, Guid principalId)
    {
        var request = new DeletePrincipalNotificationsRequest
        {
            UserPoolId = userPoolId.ToString(),
            PrincipalId = principalId.ToString()
        };

        FireAndForget.Run(logger, "Notification.DeletePrincipalNotifications",
            ct => client.DeletePrincipalNotificationsAsync(request, cancellationToken: ct).ResponseAsync);
    }
}

public static class NotificationClientExtensions
{
    /// <summary>Ayar: <c>NotificationSettings__GrpcAddress</c> (or. <c>http://booster-notification:8081</c>).</summary>
    public static IServiceCollection AddBoosterNotificationClient(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = services.AddSettings<NotificationSettings>(configuration);

        services.AddGrpcClient<NotificationDispatchService.NotificationDispatchServiceClient>(o =>
            {
                o.Address = new Uri(settings.GrpcAddress);
            })
            .ConfigureChannel(channel =>
            {
                channel.HttpHandler = new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
                    EnableMultipleHttp2Connections = true
                };
            });

        services.AddScoped<NotificationClient>();
        return services;
    }
}
