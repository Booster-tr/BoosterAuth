using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Booster.Amqp.Outbox;

/// <summary>
/// Kaydedilmis outbox satirlarini Kafka'ya gonderir.
/// <list type="bullet">
/// <item>Satirlar <c>FOR UPDATE SKIP LOCKED</c> ile alinip <see cref="Lease"/> kadar ileri atilir (kisa transaction);
/// birden fazla pod ayni satiri ayni anda gondermez. Gonderim transaction DISINDA yapilir.</item>
/// <item>Basarili gonderimde satir silinir; hata olursa artan bekleme ile (en fazla 5 dk) tekrar denenir.
/// Kafka kapaliyken satirlar bekler, kaybolmaz.</item>
/// <item>Teslim garantisi en-az-bir-kez: gonderim sirasinda pod olurse olay iki kez gidebilir.</item>
/// </list>
/// </summary>
public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopes,
    OutboxSignal<TContext> signal,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService where TContext : DbContext
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ProduceTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(5);

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, string, object, CancellationToken, Task>> Producers = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Outbox, DbContext bazinda istege baglidir (OnModelCreating'de AddBoosterOutbox). Yalnizca olay tuketen
        // servisler (Logging, Notification) cagirmaz; onlarda gonderici hic calismaz.
        using (var scope = scopes.CreateScope())
        {
            if (scope.ServiceProvider.GetRequiredService<TContext>().Model.FindEntityType(typeof(OutboxEvent)) is null)
            {
                logger.LogInformation("{Context} outbox kullanmiyor; gonderici kapali.", typeof(TContext).Name);
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Tablo henuz yok (migration oncesi) ya da veritabani gecici olarak erisilemez: sonraki turda tekrar.
                logger.LogWarning(ex, "Outbox gonderimi basarisiz; tekrar denenecek.");
            }

            // Tam dolu parti geldiyse beklemeden devam; degilse sinyal ya da kisa yoklama.
            if (processed < BatchSize)
            {
                try { await signal.WaitAsync(IdlePoll, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var table = OutboxModelBuilderExtensions.TableName;

        // Sahiplen: tek ifadede sec + kilitle + lease. Kisa, otomatik commit'li transaction.
        var batch = await db.Set<OutboxEvent>()
            .FromSqlRaw($"""
                UPDATE "{table}" SET "NextAttemptAt" = now() + make_interval(secs => {Lease.TotalSeconds}), "Attempts" = "Attempts" + 1
                WHERE "Id" IN (
                    SELECT "Id" FROM "{table}" WHERE "NextAttemptAt" <= now()
                    ORDER BY "CreatedTime" LIMIT {BatchSize} FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(ct);
        if (batch.Count == 0) return 0;

        var sent = new List<Guid>();
        foreach (var item in batch.OrderBy(x => x.CreatedTime))
        {
            try
            {
                var type = TypeCache.GetOrAdd(item.MessageType, Type.GetType)
                           ?? throw new InvalidOperationException($"Olay tipi bulunamadi: {item.MessageType}");
                var message = JsonSerializer.Deserialize(item.Payload, type, OutboxTypes.Json)
                              ?? throw new InvalidOperationException("Bos olay govdesi.");

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(ProduceTimeout);
                await Producers.GetOrAdd(type, BuildProducer)(scope.ServiceProvider, item.Key, message, timeout.Token);
                sent.Add(item.Id);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, item.Attempts), 300));
                var error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                logger.LogWarning(ex, "Outbox olayi gonderilemedi ({Type}, deneme {Attempt}); {Delay} sonra tekrar.",
                    item.MessageType, item.Attempts, delay);
                await db.Set<OutboxEvent>().Where(x => x.Id == item.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.NextAttemptAt, DateTimeOffset.UtcNow.Add(delay))
                    .SetProperty(x => x.LastError, error), CancellationToken.None);
            }
        }

        if (sent.Count > 0)
            await db.Set<OutboxEvent>().Where(x => sent.Contains(x.Id)).ExecuteDeleteAsync(CancellationToken.None);

        return batch.Count;
    }

    private static Func<IServiceProvider, string, object, CancellationToken, Task> BuildProducer(Type type) =>
        (Func<IServiceProvider, string, object, CancellationToken, Task>)ProduceMethod.MakeGenericMethod(type)
            .CreateDelegate(typeof(Func<IServiceProvider, string, object, CancellationToken, Task>));

    private static readonly MethodInfo ProduceMethod =
        typeof(OutboxDispatcher<TContext>).GetMethod(nameof(ProduceAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Task ProduceAsync<T>(IServiceProvider sp, string key, object message, CancellationToken ct) where T : class =>
        sp.GetRequiredService<ITopicProducer<string, T>>().Produce(key, (T)message, ct);
}
