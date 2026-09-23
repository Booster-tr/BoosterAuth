using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Booster.Amqp.Extensions;

public static class KafkaEndpointExtensions
{
    /// <summary>
    /// Bir topic endpoint'ine consumer'ı EF Core inbox'ıyla bağlar ve topic yoksa oluşturur.
    /// <para>
    /// <b>Inbox</b> şart: Kafka en-az-bir-kez teslim eder, yani aynı mesaj tekrar
    /// gelebilir. Inbox olmadan yeniden teslim, kaydın ikinci kez yazılması
    /// (mükerrer log, ikinci kez gönderilen bildirim) anlamına gelirdi.
    /// </para>
    /// <para>
    /// <b>CreateIfMissing</b> şart: broker'da otomatik topic oluşturma kapalıysa
    /// consumer "Unknown topic or partition" ile fault verir ve servis hiç
    /// açılmaz — ilk kurulumda topic'i elle açmayı gerektirirdi.
    /// </para>
    /// </summary>
    public static void AddConsumer<TMessage, TConsumer, TContext>(
        this IKafkaTopicReceiveEndpointConfigurator<string, TMessage> endpoint,
        IRiderRegistrationContext context)
        where TMessage : class
        where TConsumer : class, IConsumer<TMessage>
        where TContext : DbContext
    {
        endpoint.CreateIfMissing(topic =>
        {
            topic.NumPartitions = 3;

            // Geliştirme ortamında tek broker da ayakta olabiliyor; kopya sayısı
            // broker sayısını aşarsa topic hiç oluşturulamaz.
            topic.ReplicationFactor = 1;
        });

        endpoint.UseEntityFrameworkOutbox<TContext>(context);
        endpoint.ConfigureConsumer<TConsumer>(context);
    }
}
