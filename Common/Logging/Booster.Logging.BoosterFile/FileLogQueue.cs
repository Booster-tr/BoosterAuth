using System.Threading.Channels;

namespace Booster.Logging.BoosterFile;

public sealed class FileLogQueue
{
    private readonly Channel<QueuedLogEntry> _channel;

    public FileLogQueue(FileLoggingOptions options)
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
