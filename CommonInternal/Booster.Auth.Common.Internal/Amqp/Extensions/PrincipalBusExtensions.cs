using Booster.Auth.Common.Internal.Amqp.Events;
using MassTransit;

namespace Booster.Auth.Common.Internal.Amqp.Extensions;

public static class PrincipalBusExtensions
{
    public static IRiderRegistrationConfigurator AddPrincipalEventProducers(this IRiderRegistrationConfigurator rider)
    {
        rider.AddProducer<string, PrincipalDeletedEvent>(PrincipalTopics.PrincipalDeleted);
        return rider;
    }
}
