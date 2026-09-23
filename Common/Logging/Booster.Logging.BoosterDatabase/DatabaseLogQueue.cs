using System.Threading.Channels;

namespace Booster.Logging.BoosterDatabase;

public sealed class DatabaseLogQueue
{
    private readonly Channel<QueuedLogEntry> _channel;

    public DatabaseLogQueue(DatabaseLoggingOptions options)
    {
        _channel = Channel.CreateBounded<QueuedLogEntry>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    internal bool TryEnqueue(QueuedLogEntry entry) => _channel.Writer.TryWrite(entry);

    internal ChannelReader<QueuedLogEntry> Reader => _channel.Reader;
}
