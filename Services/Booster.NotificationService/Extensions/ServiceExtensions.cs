using Booster.NotificationService.Infrastructures.Retention;
using Booster.Hashing.Extensions;
using Booster.Hashing.Options;
using Booster.Auth.Common.Internal.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.NotificationService.Features._base;
using Booster.NotificationService.Infrastructures.Senders;
using Booster.NotificationService.Infrastructures.Senders._base;
using Booster.NotificationService.Infrastructures.Templating;

namespace Booster.NotificationService.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<NotificationDbContext>(configuration);

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();

        // GET health (HealthCheckEndpoint) HealthCheckService ister. Onceden MassTransit kaydediyordu; Kafka kalkinca
        // kayit da kalkti ve servis acilista coktu.
        services.AddHealthChecks();

        services.AddNotificationSenders(configuration);
        services.AddGrpc();

        services.AddScoped<NotificationBootstrapper>();

        // Gonderim kaydindaki alici ve icerik sifrelenir (IdP / Management ile ayni anahtar: HASHING_ENCRYPTION_KEY).
        var hashingOptions = services.AddSettings<HashingOptions>(configuration);
        services.AddHashing(hashingOptions);
        services.AddHostedService<NotificationRetentionService>();

        return services;
    }

    public static IServiceCollection AddNotificationSenders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettings<MailSettings>(configuration);
        services.AddSettings<SmsSettings>(configuration);
        services.AddSettings<PushSettings>(configuration);

        // Üçü de aynı arayüzle kaydedilir; consumer kanala göre olanı seçer.
        services.AddScoped<INotificationSender, SmtpEmailSender>();
        services.AddScoped<INotificationSender, SmsSender>();
        services.AddScoped<INotificationSender, PushSender>();

        services.AddScoped<ITemplateRenderer, TemplateRenderer>();

        return services;
    }
}
