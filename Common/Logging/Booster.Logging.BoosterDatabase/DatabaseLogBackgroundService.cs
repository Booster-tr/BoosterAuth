using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Booster.Logging.BoosterDatabase;

public sealed class DatabaseLogBackgroundService(DatabaseLogQueue queue, DatabaseLoggingOptions options) : BackgroundService
{
    private readonly string _qualifiedTable = $"\"{options.Schema}\".\"{options.Table}\"";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureTableAsync(stoppingToken);

        var reader = queue.Reader;
        var batch = new List<QueuedLogEntry>(options.MaxBatchSize);

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                batch.Clear();
                while (batch.Count < options.MaxBatchSize && reader.TryRead(out var entry))
                    batch.Add(entry);

                await FlushAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }

        var remaining = new List<QueuedLogEntry>();
        while (reader.TryRead(out var entry)) remaining.Add(entry);
        if (remaining.Count > 0) await FlushAsync(remaining, CancellationToken.None);
    }

    private async Task FlushAsync(List<QueuedLogEntry> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        try
        {
            await using var connection = new NpgsqlConnection(options.ConnectionString);
            await connection.OpenAsync(ct);

            var sql = new StringBuilder()
                .Append("INSERT INTO ").Append(_qualifiedTable)
                .Append(" (\"Timestamp\",\"Level\",\"Message\",\"Exception\",\"Properties\") VALUES ");

            await using var command = new NpgsqlCommand { Connection = connection };

            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                if (i > 0) sql.Append(',');
                sql.Append($"(@ts{i},@lv{i},@ms{i},@ex{i},@pr{i}::jsonb)");

                command.Parameters.AddWithValue($"ts{i}", e.Timestamp);
                command.Parameters.AddWithValue($"lv{i}", e.Level.ToString());
                command.Parameters.AddWithValue($"ms{i}", e.Message);
                command.Parameters.AddWithValue($"ex{i}", (object?)e.Exception ?? DBNull.Value);
                command.Parameters.AddWithValue($"pr{i}", (object?)Serialize(e.Properties) ?? DBNull.Value);
            }

            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // DB hatası logger'ı çökertmemeli.
        }
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        var ddl =
            $"""
            CREATE TABLE IF NOT EXISTS {_qualifiedTable} (
                "Id"        bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                "Timestamp" timestamptz NOT NULL,
                "Level"     text NOT NULL,
                "Message"   text NOT NULL,
                "Exception" text NULL,
                "Properties" jsonb NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_{options.Table}_Timestamp" ON {_qualifiedTable} ("Timestamp");
            """;

        try
        {
            await using var connection = new NpgsqlConnection(options.ConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(ddl, connection);
            await command.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Tablo oluşturulamazsa INSERT'ler de sessizce başarısız olur; logger uygulamayı düşürmez.
        }
    }

    private static string? Serialize(IReadOnlyDictionary<string, string?>? props)
        => props is { Count: > 0 } ? JsonSerializer.Serialize(props) : null;
}
