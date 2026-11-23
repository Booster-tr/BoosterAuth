using Microsoft.EntityFrameworkCore;

namespace Booster.Amqp.Outbox;

/// <summary>
/// Kafka'ya gonderilmeyi bekleyen olay (transactional outbox).
/// <para>
/// <see cref="Bus.IEventBus.PublishAsync{T}"/> bu satiri servisin DbContext'ine EKLER; satir is verisiyle AYNI
/// SaveChanges/transaction'da kaydedilir. <see cref="OutboxDispatcher{TContext}"/> kaydedilen satirlari Kafka'ya
/// gonderip siler. Kafka kapaliysa satir bekler; geri alinan islem olay uretmez.
/// </para>
/// </summary>
public sealed class OutboxEvent
{
    public Guid Id { get; set; }

    /// <summary>Olay tipi: "Tam.Ad, Assembly" (surumsuz).</summary>
    public string MessageType { get; set; } = default!;

    /// <summary>Olayin JSON hali.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>Kafka mesaj anahtari (correlation id).</summary>
    public string Key { get; set; } = string.Empty;

    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>Bir sonraki deneme zamani; gonderici satiri alirken kisa sureli kilit (lease) olarak da ileri atar.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}

public static class OutboxModelBuilderExtensions
{
    public const string TableName = "BoosterOutboxEvents";

    /// <summary>Outbox kullanan her DbContext'in OnModelCreating'inde cagrilir.</summary>
    public static ModelBuilder AddBoosterOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxEvent>(b =>
        {
            b.ToTable(TableName);
            b.HasKey(x => x.Id);
            b.Property(x => x.MessageType).HasMaxLength(512).IsRequired();
            b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.Key).HasMaxLength(128);
            b.Property(x => x.LastError).HasMaxLength(2000);
            b.HasIndex(x => x.NextAttemptAt);
        });
        return modelBuilder;
    }
}
