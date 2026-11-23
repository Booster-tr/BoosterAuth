using System.Text.Json;
using Booster.Amqp.Bus;
using Booster.Amqp.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Booster.Amqp.Outbox;

/// <summary>
/// <see cref="IEventBus"/>'in transactional outbox uygulamasi.
/// <para>
/// KURAL: olay ancak AYNI DbContext'te <c>SaveChanges</c> cagrilinca kaydedilir. SaveChanges yoksa olay HIC gitmez;
/// SaveChanges hata verirse ya da transaction geri alinirsa olay da geri alinir.
/// </para>
/// </summary>
public sealed class OutboxEventBus<TContext>(TContext db, ICorrelationContext correlation, OutboxSignal<TContext> signal)
    : IEventBus where TContext : DbContext
{
    private bool _hooked;

    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        if (db.Model.FindEntityType(typeof(OutboxEvent)) is null)
            throw new InvalidOperationException(
                $"{typeof(TContext).Name} outbox kullanmiyor: OnModelCreating'de modelBuilder.AddBoosterOutbox() cagir ve migration ekle.");

        var now = DateTimeOffset.UtcNow;
        db.Set<OutboxEvent>().Add(new OutboxEvent
        {
            Id = Guid.CreateVersion7(),
            MessageType = OutboxTypes.NameOf(typeof(T)),
            Payload = JsonSerializer.Serialize(message, typeof(T), OutboxTypes.Json),
            Key = correlation.CorrelationId,
            CreatedTime = now,
            NextAttemptAt = now,
        });

        // Kayit olunca gondericiyi hemen uyandir (beklemeden gonderim).
        if (!_hooked)
        {
            db.SavedChanges += (_, _) => signal.Notify();
            _hooked = true;
        }

        return Task.CompletedTask;
    }
}

/// <summary>Gondericiyi uyandiran sinyal (her DbContext tipi icin bir tane).</summary>
public sealed class OutboxSignal<TContext> where TContext : DbContext
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        if (_semaphore.CurrentCount == 0)
        {
            try { _semaphore.Release(); }
            catch (SemaphoreFullException) { /* zaten uyandirilmis */ }
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
}

internal static class OutboxTypes
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General);

    public static string NameOf(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";
}
