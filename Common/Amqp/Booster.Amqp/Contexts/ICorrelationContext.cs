namespace Booster.Amqp.Contexts;

public interface ICorrelationContext
{
    string CorrelationId { get; }
    void SetCorrelationId(string correlationId);
}

public class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; private set; } = string.Empty;

    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(CorrelationId))
        {
            CorrelationId = correlationId;
        }
    }
}
