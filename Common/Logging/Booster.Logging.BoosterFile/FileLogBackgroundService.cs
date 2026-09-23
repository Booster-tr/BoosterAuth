using System.Text;
using Microsoft.Extensions.Hosting;

namespace Booster.Logging.BoosterFile;

public sealed class FileLogBackgroundService(FileLogQueue queue, FileLoggingOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        System.IO.Directory.CreateDirectory(options.Directory);
        var reader = queue.Reader;
        var batch = new List<QueuedLogEntry>(options.MaxBatchSize);

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                batch.Clear();
                while (batch.Count < options.MaxBatchSize && reader.TryRead(out var entry))
                    batch.Add(entry);

                await FlushAsync(batch);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }

        // Kapanışta kuyrukta kalanları boşalt.
        var remaining = new List<QueuedLogEntry>();
        while (reader.TryRead(out var entry)) remaining.Add(entry);
        if (remaining.Count > 0) await FlushAsync(remaining);
    }

    private async Task FlushAsync(List<QueuedLogEntry> batch)
    {
        if (batch.Count == 0) return;

        // Günlük dosya bazında grupla, dosya başına tek append.
        foreach (var group in batch.GroupBy(e => Path.Combine(options.Directory, $"{options.FilePrefix}-{e.Timestamp:yyyyMMdd}.log")))
        {
            var sb = new StringBuilder();
            foreach (var entry in group)
                Append(sb, entry);

            try
            {
                await File.AppendAllTextAsync(group.Key, sb.ToString());
            }
            catch
            {
                // Disk hatası logger'ı çökertmemeli.
            }
        }
    }

    private static void Append(StringBuilder sb, QueuedLogEntry entry)
    {
        sb.Append(entry.Timestamp.ToString("O")).Append(" [").Append(entry.Level).Append("] ").Append(entry.Message);

        if (entry.Properties is { Count: > 0 })
        {
            sb.Append(" {");
            var first = true;
            foreach (var (key, value) in entry.Properties)
            {
                if (!first) sb.Append(", ");
                sb.Append(key).Append('=').Append(value);
                first = false;
            }
            sb.Append('}');
        }

        if (entry.Exception is not null)
            sb.Append(Environment.NewLine).Append(entry.Exception);

        sb.Append(Environment.NewLine);
    }
}
