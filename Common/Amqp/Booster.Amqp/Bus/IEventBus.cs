namespace Booster.Amqp.Bus;

/// <summary>
/// Is olaylari (e-posta, bildirim vb.) icin transactional outbox.
/// <para>
/// KURAL: olay servisin DbContext'ine eklenir ve ancak AYNI DbContext'te <c>SaveChanges</c> cagrilinca kaydedilir.
/// SaveChanges yoksa olay HIC gitmez; islem geri alinirsa olay da geri alinir. Kafka kapaliysa olay bekler.
/// Kayip kabul edilebilen olaylar (loglar) icin <see cref="IDirectEventBus"/>.
/// </para>
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
