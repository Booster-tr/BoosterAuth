using Booster.Logging.Abstraction;

namespace Booster.Logging.BoosterFile;

public class FileLogWriter(FileLogQueue queue, FileLoggingOptions options) : ILogWriter
{
    public Task LogAsync(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
    {
        if (level < options.MinimumLevel)
            return Task.CompletedTask;

        
        IReadOnlyDictionary<string, string?>? snapshot =
            properties is { Count: > 0 } ? new Dictionary<string, string?>(properties) : null;

        queue.TryEnqueue(new QueuedLogEntry(DateTimeOffset.UtcNow, level, message, exception?.ToString(), snapshot));
        return Task.CompletedTask;
    }

}
