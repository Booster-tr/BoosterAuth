using Booster.Logging.Abstraction;

namespace Booster.Logging.BoosterDatabase;

internal sealed record QueuedLogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, string?>? Properties);
