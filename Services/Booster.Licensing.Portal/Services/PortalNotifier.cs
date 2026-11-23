using System.Globalization;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Common.Internal.Constants;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Portal.Options;
using IEventBus = Booster.Amqp.Bus.IEventBus;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Musteri e-postalari. Booster'in Notification servisi gonderir (Kafka: notification-requested-topic).
/// <para>
/// Olay Kafka'ya HEMEN gider: Kafka rider ureticileri EF outbox'i kullanmaz (yerelde dogrulandi: OutboxMessage
/// tablosuna hic satir yazilmiyor). Bu yuzden cagiran, is islemini KAYDETTIKTEN / commit ettikten SONRA cagirir;
/// geri alinan bir islem e-posta gondermemis olur.
/// </para>
/// E-posta en-iyi-cabadir: Kafka hatasi loglanir ve <c>false</c> doner, is islemi bozulmaz (lisans portalda zaten
/// gorunur). Alici firmanin fatura e-postasidir (<see cref="Customer.ContactEmail"/>).
/// </summary>
public sealed class PortalNotifier(IEventBus eventBus, LicensingPortalOptions options, ILogger<PortalNotifier> logger)
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private string Url(string path) => $"{options.PublicUrl.TrimEnd('/')}{path}";
    private static string Date(DateTimeOffset d) => d.ToString("dd.MM.yyyy", Tr);
    private static string Money(decimal amount, string currency) =>
        amount > 0 ? $"{amount.ToString("N2", Tr)} {currency}" : "Size ayrıca bildirilecek";

    public Task<bool> TrialIssuedAsync(Customer customer, IssuedLicense license, CancellationToken ct) =>
        SendAsync(customer, NotificationTemplateKeys.LicenseTrialIssued, new()
        {
            ["CustomerName"] = customer.Name,
            ["InstallationId"] = license.InstallationId.ToString(),
            ["ExpiresAt"] = Date(license.ExpiresAt),
            ["Token"] = license.Token,
            ["PortalUrl"] = Url($"/hesap/lisans?LicenseId={license.Id}"),
        }, ct);

    public Task<bool> OrderReceivedAsync(Order order, CancellationToken ct) =>
        SendAsync(order.BillingEmail, NotificationTemplateKeys.LicenseOrderReceived, new()
        {
            ["CustomerName"] = order.BillingName,
            ["ReferenceCode"] = order.ReferenceCode,
            ["PlanName"] = order.PlanName,
            ["Amount"] = Money(order.Price, order.Currency),
            ["BankName"] = options.Bank.BankName,
            ["AccountName"] = options.Bank.AccountName,
            ["Iban"] = options.Bank.Iban,
            ["OrderUrl"] = Url($"/hesap/siparis?OrderId={order.Id}"),
        }, ct);

    public Task<bool> LicenseIssuedAsync(Order order, IssuedLicense license, CancellationToken ct) =>
        SendAsync(order.BillingEmail, NotificationTemplateKeys.LicenseIssued, new()
        {
            ["CustomerName"] = order.BillingName,
            ["ReferenceCode"] = order.ReferenceCode,
            ["PlanName"] = order.PlanName,
            ["ExpiresAt"] = Date(license.ExpiresAt),
            ["Token"] = license.Token,
            ["LicenseUrl"] = Url($"/hesap/lisans?LicenseId={license.Id}"),
        }, ct);

    public Task<bool> RenewalReminderAsync(Customer customer, IssuedLicense license, int daysLeft, CancellationToken ct) =>
        SendAsync(customer, NotificationTemplateKeys.LicenseRenewalReminder, new()
        {
            ["CustomerName"] = customer.Name,
            ["InstallationId"] = license.InstallationId.ToString(),
            ["PlanName"] = license.Plan,
            ["ExpiresAt"] = Date(license.ExpiresAt),
            ["DaysLeft"] = daysLeft.ToString(Tr),
            ["RenewUrl"] = Url($"/hesap/siparis/yeni?InstallationId={license.InstallationId}"),
        }, ct);

    public Task<bool> TransferredAsync(Customer customer, IssuedLicense from, IssuedLicense to, string requestedBy, CancellationToken ct) =>
        SendAsync(customer, NotificationTemplateKeys.LicenseTransferred, new()
        {
            ["CustomerName"] = customer.Name,
            ["PlanName"] = to.Plan,
            ["FromInstallationId"] = from.InstallationId.ToString(),
            ["ToInstallationId"] = to.InstallationId.ToString(),
            ["RequestedBy"] = requestedBy,
            ["TransferredAt"] = DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm 'UTC'", Tr),
            ["Token"] = to.Token,
            ["LicenseUrl"] = Url($"/hesap/lisans?LicenseId={to.Id}"),
        }, ct);

    private Task<bool> SendAsync(Customer customer, string template, Dictionary<string, string> data, CancellationToken ct) =>
        SendAsync(customer.ContactEmail, template, data, ct);

    private async Task<bool> SendAsync(string to, string template, Dictionary<string, string> data, CancellationToken ct)
    {
        try
        {
            await Publish(to, template, data, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Alici adresi loglanmaz (kisisel veri).
            logger.LogError(ex, "Musteri e-postasi kuyruga yazilamadi: {Template}", template);
            return false;
        }
    }

    private Task Publish(string to, string template, Dictionary<string, string> data, CancellationToken ct) =>
        eventBus.PublishAsync(new NotificationRequestedEvent
        {
            TenantId = Guid.Empty,
            // Sablon secimi havuza gore yapilir (havuza ozel sablon yoksa genel sablon).
            UserPoolId = AuthConstants.LicensingPoolId,
            TemplateKey = template,
            Channel = NotificationChannel.Email,
            Target = to,
            LanguageCode = "tr-tr",
            Data = data,
        }, ct);
}
