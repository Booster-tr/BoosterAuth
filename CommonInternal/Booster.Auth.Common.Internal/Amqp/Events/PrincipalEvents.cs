namespace Booster.Auth.Common.Internal.Amqp.Events;

/// <summary>
/// Bir kullanici hesabi silindi (panelden ya da kendi istegiyle). IdP uretir; kisisel veri tutan servisler (Notification:
/// gonderilen e-posta / SMS kayitlari) o kullanicinin kayitlarini siler. Olay commit'ten SONRA yayinlanir.
/// </summary>
public record PrincipalDeletedEvent
{
    public Guid UserPoolId { get; init; }
    public Guid PrincipalId { get; init; }
}

public static class PrincipalTopics
{
    public const string PrincipalDeleted = "auth-principal-deleted-topic";
}
