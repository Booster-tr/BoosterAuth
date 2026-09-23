using Booster.Amqp.Bus;
using Booster.Logging.Abstraction;

namespace Booster.Logging.BoosterAmqp;


public class AmqpLogWriter(IEventBus producer) : ILogPublisher
{
    public async Task LogAsync(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
    {
        var logEvent = new LogCreateEvent
        {
            Level = level.ToString(),
            Message = message,
            Exception = exception?.ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Properties = ToStringMap(properties)
        };

        try
        {
            await producer.PublishAsync(logEvent, ct);
        }
        catch
        {

        }
    }

  
    private static Dictionary<string, string> ToStringMap(IReadOnlyDictionary<string, string?>? properties)
    {
        var map = new Dictionary<string, string>();
        if (properties is null) return map;

        foreach (var (key, value) in properties)
            if (value is not null)
                map[key] = value.ToString() ?? string.Empty;

        return map;
    }
}
