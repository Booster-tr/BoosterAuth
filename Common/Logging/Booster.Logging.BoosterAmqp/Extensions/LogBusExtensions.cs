using MassTransit;

namespace Booster.Logging.BoosterAmqp.Extensions;

/// <summary>
/// Host'un MassTransit Kafka kurulumunda log producer/consumer'ını tek satırda
/// bağlayan yardımcılar.
/// <para>
/// Otomatik dinleme yoktur: MassTransit Kafka'da consumer'ın hem rider'a
/// eklenmesi hem de bir TopicEndpoint'e bağlanması gerekir. İkisi ayrı adım
/// olduğu için her biri için ayrı bir metot var.
/// </para>
/// </summary>
public static class LogBusExtensions
{
    /// <summary>
    /// Log <b>producer</b>'ını kaydeder. Log yazan her servis rider kurulumunda
    /// çağırmalı; yoksa <c>ITopicProducer</c> DI'dan çözülemez ve ilk log
    /// denemesinde patlar.
    /// </summary>
    public static IRiderRegistrationConfigurator AddLogProducer(this IRiderRegistrationConfigurator rider)
    {
        rider.AddProducer<string, LogCreateEvent>(LogTopics.LogCreate);
        return rider;
    }

    /// <summary>Log consumer'ını rider'a kaydeder (endpoint için <see cref="AddLogConsumerEndpoint{TConsumer}"/>).</summary>
    public static IRiderRegistrationConfigurator AddLogConsumer<TConsumer>(this IRiderRegistrationConfigurator rider)
        where TConsumer : class, IConsumer<LogCreateEvent>
    {
        rider.AddConsumer<TConsumer>();
        return rider;
    }

    /// <summary>
    /// <see cref="LogTopics.LogCreate"/> topic'ini consumer'a bağlar.
    /// <para>
    /// <paramref name="configureEndpoint"/> verilirse consumer'ı bağlamak
    /// ÇAĞIRANIN sorumluluğundadır; hem burada hem orada bağlanırsa aynı mesaj
    /// iki kez işlenir ve inbox'ta çift anahtar hatası çıkar.
    /// </para>
    /// </summary>
    public static void AddLogConsumerEndpoint<TConsumer>(
        this IKafkaFactoryConfigurator cfg,
        IRiderRegistrationContext context,
        string groupId,
        Action<IKafkaTopicReceiveEndpointConfigurator<string, LogCreateEvent>>? configureEndpoint = null)
        where TConsumer : class, IConsumer<LogCreateEvent>
    {
        cfg.TopicEndpoint<string, LogCreateEvent>(LogTopics.LogCreate, groupId, e =>
        {
            if (configureEndpoint is not null)
                configureEndpoint(e);
            else
                e.ConfigureConsumer<TConsumer>(context);
        });
    }
}
