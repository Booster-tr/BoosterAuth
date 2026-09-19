using Booster.Amqp.Contexts;
using Booster.Amqp.Providers.ProducerProvider;

namespace Booster.Amqp.Bus;

public class MassTransitEventBus(ICorrelationContext correlationContext, IKafkaProducerProvider kafkaProvider) : IEventBus
{
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        var producer = kafkaProvider.GetProducer<T>();
        await producer.Produce(correlationContext.CorrelationId, message, ct);
    }
}
