namespace Booster.Logging.BoosterAmqp;

public class LogCreateEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = default!;
    public string? Exception { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}
