using MassTransit;

namespace Booster.Amqp.Providers.ProducerProvider;

public interface IKafkaProducerProvider
{
    ITopicProducer<string, T> GetProducer<T>() where T : class;
}
