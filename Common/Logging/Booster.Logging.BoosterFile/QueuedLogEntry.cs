using Booster.Logging.Abstraction;

namespace Booster.Logging.BoosterFile;

/// <summary>Kuyruğa alınan log kaydının anlık görüntüsü (enqueue anında damgalanır).</summary>
internal sealed record QueuedLogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, string?>? Properties);
