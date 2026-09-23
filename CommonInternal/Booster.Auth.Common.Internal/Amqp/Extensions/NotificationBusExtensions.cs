using Booster.Amqp.Constants;
using Booster.Auth.Common.Internal.Amqp.Events;
using MassTransit;

namespace Booster.Auth.Common.Internal.Amqp.Extensions;

public static class NotificationBusExtensions
{
    public static IRiderRegistrationConfigurator AddNotificationProducer(
        this IRiderRegistrationConfigurator rider)
    {
        rider.AddProducer<string, NotificationRequestedEvent>(TopicDef.NotificationRequestedEventTopic);
        return rider;
    }
}
