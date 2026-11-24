using Booster.Auth.Common.Internal.Amqp.Events;
using MassTransit;

namespace Booster.Auth.Common.Internal.Amqp.Extensions;

public static class PoolDefinitionBusExtensions
{
    public static IRiderRegistrationConfigurator AddPoolDefinitionProducers(
        this IRiderRegistrationConfigurator rider)
    {
        rider.AddProducer<string, ScopeDefinitionChangedEvent>(PoolDefinitionTopics.ScopeDefinitionChanged);
        rider.AddProducer<string, RoleChangedEvent>(PoolDefinitionTopics.RoleChanged);
        return rider;
    }
}
