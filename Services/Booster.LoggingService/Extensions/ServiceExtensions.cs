using Booster.Amqp.Constants;
using Booster.Amqp.Extensions;
using Booster.Auth.Common.Internal.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.Logging.BoosterAmqp;
using Booster.Logging.BoosterAmqp.Extensions;
using Booster.LoggingService.Amqp.Consumers;
using Booster.LoggingService.Features._base;

namespace Booster.LoggingService.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddLoggingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<LoggerDbContext>(configuration);

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();

        services.AddKafka(configuration);

        return services;
    }

    /// <summary>
    /// Bu servis log ÜRETMEZ, TÜKETİR: topic'i dinleyip veritabanına yazar.
    /// Kendi logunu da aynı topic'e yayınlasaydı işlediği her kayıt yeni bir log
    /// doğurur ve kuyruk kendi kendini beslerdi.
    /// </summary>
    public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
    {
        var amqpSettings = services.AddSettings<AmqpSettings>(configuration);

        services.AddBoosterCorrelationContext();

        services.AddKafkaBus<LoggerDbContext>(
            amqpSettings,
            configureRider: rider =>
            {
                rider.AddLogConsumer<LogPersistConsumer>();
            },
            configureKafka: (cfg, context) =>
            {
                cfg.AddLogConsumerEndpoint<LogPersistConsumer>(
                    context,
                    amqpSettings.GroupId,
                    e => e.AddConsumer<LogCreateEvent, LogPersistConsumer, LoggerDbContext>(context));
            });

        return services;
    }
}
