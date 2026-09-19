namespace Booster.Amqp.Constants;

public class AmqpSettings
{
    public string ServiceName { get; set; } = "unknown";
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "default-group";
}
