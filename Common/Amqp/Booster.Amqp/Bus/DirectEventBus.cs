using Booster.Amqp.Contexts;
using Booster.Amqp.Providers.ProducerProvider;

namespace Booster.Amqp.Bus;

/// <summary>
/// Olayi outbox'a YAZMADAN, hemen Kafka'ya gonderir. Yalnizca kaybi kabul edilebilir, bir is islemine bagli olmayan
/// olaylar icindir (loglar). Kafka kapaliysa olay kaybolabilir; is olaylari icin <see cref="IEventBus"/> kullan.
/// </summary>
public interface IDirectEventBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}

public class DirectEventBus(ICorrelationContext correlationContext, IKafkaProducerProvider kafkaProvider) : IDirectEventBus
{
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        var producer = kafkaProvider.GetProducer<T>();
        await producer.Produce(correlationContext.CorrelationId, message, ct);
    }
}
