namespace Booster.Logging.Abstraction;

public static class LogWriterExtensions
{
    public static Task LogTraceAsync(this ILogWriter writer, string message, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
        => writer.LogAsync(LogLevel.Trace, message, null, properties, ct);

    public static Task LogDebugAsync(this ILogWriter writer, string message, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
        => writer.LogAsync(LogLevel.Debug, message, null, properties, ct);

    public static Task LogInformationAsync(this ILogWriter writer, string message, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
        => writer.LogAsync(LogLevel.Information, message, null, properties, ct);

    public static Task LogWarningAsync(this ILogWriter writer, string message, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
        => writer.LogAsync(LogLevel.Warning, message, null, properties, ct);

    public static Task LogErrorAsync(this ILogWriter writer, string message, Exception? exception = null, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
        => writer.LogAsync(LogLevel.Error, message, exception, properties, ct);

    public static Task LogCriticalAsync(this ILogWriter writer, string message, Exception? exception = null, IReadOnlyDictionary<string, string?>? properties = null, CancellationToken ct = default)
        => writer.LogAsync(LogLevel.Critical, message, exception, properties, ct);
}
