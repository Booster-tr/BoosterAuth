namespace Booster.Amqp.Constants;

/// <summary>
/// Sistemdeki Kafka topic adları.
/// <para>
/// Tek kaynakta durmak zorunda: üreten ve tüketen iki ayrı servis olduğu için
/// adlar iki yerde ayrı yazılsaydı bir harflik fark, mesajın sessizce hiç
/// teslim edilmemesi demek olurdu — hata da alınmazdı.
/// </para>
/// </summary>
public static class TopicDef
{
    /// <summary>Uygulama logları.</summary>
    public const string CreateLogEventTopic = "log-create-topic";


    /// <summary>Bildirim gönderme istekleri.</summary>
    public const string NotificationRequestedEventTopic = "notification-requested-topic";
}
