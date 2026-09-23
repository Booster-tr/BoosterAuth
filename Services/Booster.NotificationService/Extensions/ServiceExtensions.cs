using Booster.Amqp.Constants;
using Booster.Amqp.Extensions;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Common.Internal.Amqp.Filters;
using Booster.Auth.Common.Internal.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.Logging.BoosterAmqp.Extensions;
using Booster.NotificationService.Features._amqp.Consumers.NotificationRequested;
using Booster.NotificationService.Features._base;
using Booster.NotificationService.Infrastructures.Senders;
using Booster.NotificationService.Infrastructures.Senders._base;
using Booster.NotificationService.Infrastructures.Templating;
using MassTransit;

namespace Booster.NotificationService.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<NotificationDbContext>(configuration);

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();

        services.AddNotificationSenders(configuration);
        services.AddKafka(configuration);

        services.AddScoped<NotificationBootstrapper>();

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

    public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
    {
        var amqpSettings = services.AddSettings<AmqpSettings>(configuration);

        services.AddBoosterCorrelationContext();
        services.AddAmqpLogger();

        services.AddKafkaBus<NotificationDbContext>(
            amqpSettings,
            configureRider: rider =>
            {
                // Log yazan her servis producer kaydını yapmalı; yoksa
                // ITopicProducer çözülemez ve yazma anında patlar.
                rider.AddLogProducer();

                rider.AddConsumer<NotificationRequestedEventConsumer>();
            },
            configureKafka: (cfg, context) =>
            {
                cfg.UseSendFilter(typeof(CorrelationPublishFilter<>), context);
                cfg.UseSendFilter(typeof(RequestContextPublishFilter<>), context);

                cfg.TopicEndpoint<string, NotificationRequestedEvent>(
                    TopicDef.NotificationRequestedEventTopic,
                    amqpSettings.GroupId,
                    e => e.AddConsumer<NotificationRequestedEvent, NotificationRequestedEventConsumer, NotificationDbContext>(context));
            });

        return services;
    }
}
