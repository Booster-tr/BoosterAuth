namespace Booster.Auth.Common.Internal.Amqp.Events;

public enum NotificationChannel
{
    Email = 1,
    Sms = 2,
    Push = 3
}

public class NotificationRequestedEvent
{
    public Guid TenantId { get; set; }

    public Guid UserPoolId { get; set; }

    public Guid? PrincipalId { get; set; }

    public string TemplateKey { get; set; } = default!;

    public NotificationChannel Channel { get; set; }

    public string Target { get; set; } = default!;

    public string LanguageCode { get; set; } = "tr-tr";

    public Dictionary<string, string> Data { get; set; } = [];
}

public static class NotificationTemplateKeys
{
    public const string EmailVerification = "email_verification";
    public const string PasswordReset = "password_reset";
    public const string MfaCode = "mfa_code";
    public const string PhoneVerification = "phone_verification";
    public const string UserInvitation = "user_invitation";

    public const string EmailLoginCode = "email_login_code";

    public const string PhoneLoginCode = "phone_login_code";
}
