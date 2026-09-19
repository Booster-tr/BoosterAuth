using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Amqp.Providers.ProducerProvider;

public class KafkaProducerProvider(IServiceProvider serviceProvider) : IKafkaProducerProvider
{
    public ITopicProducer<string, T> GetProducer<T>() where T : class
        => serviceProvider.GetRequiredService<ITopicProducer<string, T>>();
}
