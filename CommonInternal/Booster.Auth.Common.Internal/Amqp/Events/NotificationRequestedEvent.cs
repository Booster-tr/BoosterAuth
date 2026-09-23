namespace Booster.Auth.Common.Internal.Amqp.Events;

/// <summary>Bildirimin hangi kanaldan gideceği.</summary>
public enum NotificationChannel
{
    Email = 1,
    Sms = 2,
    Push = 3
}

/// <summary>
/// Bildirim gönderme isteği. Üreten servis (IdP, Management…) ile tüketen
/// (NotificationService) arasındaki sözleşme; bu yüzden iki tarafın da gördüğü
/// ortak projede durur.
/// <para>
/// Metnin kendisi TAŞINMAZ, yalnızca <see cref="TemplateKey"/> ve
/// <see cref="Data"/> taşınır: metin şablondan çözülür, böylece bir e-postanın
/// içeriğini değiştirmek için üreten servisleri yeniden yayınlamak gerekmez.
/// </para>
/// </summary>
public class NotificationRequestedEvent
{
    public Guid TenantId { get; set; }

    public Guid UserPoolId { get; set; }

    /// <summary>Alıcı kullanıcı; sistem bildirimlerinde boş olabilir.</summary>
    public Guid? PrincipalId { get; set; }

    public string TemplateKey { get; set; } = default!;

    public NotificationChannel Channel { get; set; }

    /// <summary>E-posta adresi, telefon numarası ya da cihaz jetonu.</summary>
    public string Target { get; set; } = default!;

    public string LanguageCode { get; set; } = "tr-tr";

    /// <summary>Şablondaki <c>{{Placeholder}}</c> alanlarını dolduran değerler.</summary>
    public Dictionary<string, string> Data { get; set; } = [];
}

/// <summary>Bildirim şablon anahtarları. Üreten ve seed eden taraf aynı adı kullanır.</summary>
public static class NotificationTemplateKeys
{
    public const string EmailVerification = "email_verification";
    public const string PasswordReset = "password_reset";
    public const string MfaCode = "mfa_code";
    public const string PhoneVerification = "phone_verification";
    public const string UserInvitation = "user_invitation";
}
