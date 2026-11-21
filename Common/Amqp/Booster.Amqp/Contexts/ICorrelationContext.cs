namespace Booster.Amqp.Contexts;

public interface ICorrelationContext
{
    string CorrelationId { get; }
    void SetCorrelationId(string correlationId);
}

/// <summary>
/// O anki isteğin correlation id'si.
/// <para>
/// Singleton kayıtlıdır ama değer <see cref="AsyncLocal{T}"/>'ta tutulur: her istek (async akış)
/// kendi id'sini görür. Düz bir alan olsaydı süreçteki İLK isteğin id'si sonsuza dek kalır,
/// bütün loglar ve Kafka mesajları aynı id ile işaretlenirdi.
/// </para>
/// </summary>
public class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public string CorrelationId => Current.Value ?? string.Empty;

    public void SetCorrelationId(string correlationId) => Current.Value = correlationId;
}
