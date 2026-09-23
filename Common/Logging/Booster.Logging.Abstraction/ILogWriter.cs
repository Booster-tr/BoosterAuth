namespace Booster.Logging.Abstraction;

public interface ILogWriter
{
    Task LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? properties = null,
        CancellationToken ct = default);

}
