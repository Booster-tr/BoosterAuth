namespace Booster.NotificationService.Infrastructures.Senders._base;

public class MailSettings
{
    public string Sender { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string SmtpHost { get; set; } = default!;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public bool EnableSsl { get; set; } = true;
}

public class SmsSettings
{
    public string Endpoint { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string Originator { get; set; } = default!;
}

public class PushSettings
{
    /// <summary>Firebase service account JSON dosyasının yolu.</summary>
    public string CredentialsPath { get; set; } = default!;
    public string ProjectId { get; set; } = default!;
}
