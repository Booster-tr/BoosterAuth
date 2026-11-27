using System.Globalization;
using Booster.Auth.Common.Internal.Notifications;
using Booster.Auth.Common.Internal.Constants;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Portal.Options;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Musteri e-postalari. Booster'in Notification servisi gonderir (gRPC, <see cref="NotificationClient"/>).
/// <para>
/// Cagri BEKLENMEZ (fire-and-forget): Notification kapaliysa e-posta kaybolur, is islemi bozulmaz (lisans portalda
/// zaten gorunur). Cagiran is islemini KAYDETTIKTEN / commit ettikten SONRA cagirir; geri alinan bir islem e-posta
/// gondermemis olur. Alici firmanin fatura e-postasidir (<see cref="Customer.ContactEmail"/>).
/// </para>
/// </summary>
public sealed class PortalNotifier(NotificationClient notifications, LicensingPortalOptions options)
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private string Url(string path) => $"{options.PublicUrl.TrimEnd('/')}{path}";
    private static string Date(DateTimeOffset d) => d.ToString("dd.MM.yyyy", Tr);
    private static string Money(decimal amount, string currency) =>
        amount > 0 ? $"{amount.ToString("N2", Tr)} {currency}" : "Size ayrıca bildirilecek";

    public void TrialIssued(Customer customer, IssuedLicense license) =>
        Send(customer, NotificationTemplateKeys.LicenseTrialIssued, new()
        {
            ["CustomerName"] = customer.Name,
            ["InstallationId"] = license.InstallationId.ToString(),
            ["ExpiresAt"] = Date(license.ExpiresAt),
            ["Token"] = license.Token,
            ["PortalUrl"] = Url($"/hesap/lisans?LicenseId={license.Id}"),
        });

    public void OrderReceived(Order order) =>
        Send(order.BillingEmail, NotificationTemplateKeys.LicenseOrderReceived, new()
        {
            ["CustomerName"] = order.BillingName,
            ["ReferenceCode"] = order.ReferenceCode,
            ["PlanName"] = order.PlanName,
            ["Amount"] = Money(order.Price, order.Currency),
            ["BankName"] = options.Bank.BankName,
            ["AccountName"] = options.Bank.AccountName,
            ["Iban"] = options.Bank.Iban,
            ["OrderUrl"] = Url($"/hesap/siparis?OrderId={order.Id}"),
        });

    public void LicenseIssued(Order order, IssuedLicense license) =>
        Send(order.BillingEmail, NotificationTemplateKeys.LicenseIssued, new()
        {
            ["CustomerName"] = order.BillingName,
            ["ReferenceCode"] = order.ReferenceCode,
            ["PlanName"] = order.PlanName,
            ["ExpiresAt"] = Date(license.ExpiresAt),
            ["Token"] = license.Token,
            ["LicenseUrl"] = Url($"/hesap/lisans?LicenseId={license.Id}"),
        });

    public void RenewalReminder(Customer customer, IssuedLicense license, int daysLeft) =>
        Send(customer, NotificationTemplateKeys.LicenseRenewalReminder, new()
        {
            ["CustomerName"] = customer.Name,
            ["InstallationId"] = license.InstallationId.ToString(),
            ["PlanName"] = license.Plan,
            ["ExpiresAt"] = Date(license.ExpiresAt),
            ["DaysLeft"] = daysLeft.ToString(Tr),
            ["RenewUrl"] = Url($"/hesap/siparis/yeni?InstallationId={license.InstallationId}"),
        });

    public void Transferred(Customer customer, IssuedLicense from, IssuedLicense to, string requestedBy) =>
        Send(customer, NotificationTemplateKeys.LicenseTransferred, new()
        {
            ["CustomerName"] = customer.Name,
            ["PlanName"] = to.Plan,
            ["FromInstallationId"] = from.InstallationId.ToString(),
            ["ToInstallationId"] = to.InstallationId.ToString(),
            ["RequestedBy"] = requestedBy,
            ["TransferredAt"] = DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm 'UTC'", Tr),
            ["Token"] = to.Token,
            ["LicenseUrl"] = Url($"/hesap/lisans?LicenseId={to.Id}"),
        });

    private void Send(Customer customer, string template, Dictionary<string, string> data) =>
        Send(customer.ContactEmail, template, data);

    private void Send(string to, string template, Dictionary<string, string> data) =>
        notifications.Send(new NotificationRequest
        {
            TenantId = Guid.Empty,
            // Sablon secimi havuza gore yapilir (havuza ozel sablon yoksa genel sablon).
            UserPoolId = AuthConstants.LicensingPoolId,
            TemplateKey = template,
            Channel = NotificationChannel.Email,
            Target = to,
            LanguageCode = "tr-tr",
            Data = data,
        });
}
