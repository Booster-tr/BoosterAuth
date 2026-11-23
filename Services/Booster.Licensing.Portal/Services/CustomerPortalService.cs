using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Services;

/// <summary>Self-servis isleminin sonucu: basarili ise lisans; degilse kullaniciya gosterilecek sebep anahtari.</summary>
public sealed record SelfServiceResult(IssuedLicense? License, string? Error)
{
    public static SelfServiceResult Fail(string error) => new(null, error);
}

/// <summary>Sebep anahtarlari (gorunum metinleri SelfServiceErrors.Text icinde).</summary>
public static class SelfServiceErrors
{
    public const string FreeEmail = "free_email";
    public const string EmailDomainMismatch = "email_domain_mismatch";
    public const string DomainAlreadyTrialed = "domain_already_trialed";
    public const string IpLimit = "ip_limit";
    public const string NotFound = "not_found";
    public const string NotTransferable = "not_transferable";
    public const string SameInstallation = "same_installation";
    public const string TransferLimit = "transfer_limit";

    public static string Text(string key) => key switch
    {
        FreeEmail => "Ücretsiz e-posta adresleriyle deneme lisansı verilmiyor. Lütfen firmanızın e-posta adresiyle kayıt olun.",
        EmailDomainMismatch => "E-posta adresinizin domain'i bu kurulumun domain'leriyle uyuşmuyor. Deneme yalnızca firmanın kendi domain'i için verilir.",
        DomainAlreadyTrialed => "Bu domain için daha önce deneme lisansı verilmiş. Devam etmek için lisans satın alabilirsiniz.",
        IpLimit => "Bugün çok fazla deneme isteği yapıldı. Lütfen yarın tekrar deneyin.",
        NotFound => "Kayıt bulunamadı.",
        NotTransferable => "Bu lisans aktarılamaz (deneme lisansı, süresi dolmuş ya da zaten aktarılmış).",
        SameInstallation => "Lisans zaten bu kurulumda.",
        TransferLimit => "Bu lisans son bir yıl içinde 3 kez aktarıldı; yeni aktarım yapılamaz.",
        _ => "İşlem yapılamadı.",
    };
}

/// <summary>
/// Musteri self-servis kurallari: deneme lisansi ve aktarim. Kontroller burada; controller yalnizca cagirir.
/// </summary>
public sealed class CustomerPortalService(LicensingDbContext db, LicenseIssuer issuer, TimeProvider time, PortalNotifier notifier)
{
    /// <summary>
    /// Ucretsiz deneme. Kurallar (sirayla):
    /// ucretsiz e-posta reddedilir; e-posta domaini kurulum domainlerinden biriyle uyusmali; ayni domaine (alt alan dahil)
    /// daha once deneme verilmisse reddedilir; IP basina gunde en fazla 3 istek. Her istek (red dahil) kaydedilir.
    /// </summary>
    public async Task<SelfServiceResult> RequestTrialAsync(Customer customer, CustomerInstallation installation,
        string email, string? ip, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var emailDomain = SelfServiceRules.EmailDomain(email);

        string? error = null;
        if (emailDomain is null || SelfServiceRules.FreeEmailDomains.Contains(emailDomain))
            error = SelfServiceErrors.FreeEmail;
        else if (!installation.Domains.Any(d => SelfServiceRules.DomainsRelated(emailDomain, d)))
            error = SelfServiceErrors.EmailDomainMismatch;
        else if (await DomainAlreadyTrialedAsync(installation.Domains, ct))
            error = SelfServiceErrors.DomainAlreadyTrialed;
        else if (ip is not null && await db.TrialRequests.CountAsync(r => r.IpAddress == ip && r.CreatedTime > now.AddDays(-1), ct)
                 >= SelfServiceRules.TrialRequestsPerIpPerDay)
            error = SelfServiceErrors.IpLimit;

        var request = new TrialRequest
        {
            Id = EntityId.New(),
            CustomerId = customer.Id,
            InstallationId = installation.InstallationId,
            IpAddress = ip,
            Email = email,
            Domains = [.. installation.Domains],
            Granted = error is null,
            RejectReason = error,
        };
        db.TrialRequests.Add(request);

        if (error is not null)
        {
            await db.SaveChangesAsync(ct);
            return SelfServiceResult.Fail(error);
        }

        var license = await issuer.IssueAsync(new IssueLicenseRequest(
            customer.Id, installation.InstallationId, "trial", LicenseTypes.Trial,
            LicenseIssuer.Defaults.TrialUsers, LicenseIssuer.Defaults.TrialPools, LicenseIssuer.Defaults.TrialClients,
            installation.Domains, LicenseIssuer.Defaults.TrialDays, LicenseIssuer.Defaults.TrialGraceDays,
            "self-service", "Portal: ucretsiz deneme"), ct);

        request.LicenseId = license.Id;
        await notifier.TrialIssuedAsync(customer, license, ct);
        await db.SaveChangesAsync(ct);
        return new SelfServiceResult(license, null);
    }

    /// <summary>
    /// Lisansi yeni bir kuruluma aktarir: ayni plan, limitler, domainler ve BITIS TARIHI. Lisans basina yilda en fazla 3.
    /// <para>
    /// Not: model cevrimdisidir; eski kurulumdaki lisans o sunucuda suresi dolana kadar calismaya devam eder (urun
    /// lisans sunucusuna baglanmaz, iptal edilemez). Yillik sinir bu yuzden vardir.
    /// </para>
    /// </summary>
    public async Task<SelfServiceResult> TransferAsync(Customer customer, Guid licenseId, CustomerInstallation target,
        string requestedBy, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var license = await db.IssuedLicenses.SingleOrDefaultAsync(l => l.Id == licenseId && l.CustomerId == customer.Id, ct);
        if (license is null) return SelfServiceResult.Fail(SelfServiceErrors.NotFound);

        if (license.Type != LicenseTypes.Paid || license.TransferredToLicenseId is not null || license.ExpiresAt <= now)
            return SelfServiceResult.Fail(SelfServiceErrors.NotTransferable);
        if (license.InstallationId == target.InstallationId)
            return SelfServiceResult.Fail(SelfServiceErrors.SameInstallation);

        var origin = license.OriginLicenseId ?? license.Id;
        var lastYear = await db.LicenseTransfers.CountAsync(t => t.OriginLicenseId == origin && t.CreatedTime > now.AddYears(-1), ct);
        if (lastYear >= SelfServiceRules.TransfersPerYear)
            return SelfServiceResult.Fail(SelfServiceErrors.TransferLimit);

        var moved = await issuer.IssueAsync(new IssueLicenseRequest(
            customer.Id, target.InstallationId, license.Plan, license.Type,
            license.MaxUsers, license.MaxPools, license.MaxClients, license.AllowedDomains,
            1, license.GraceDays, requestedBy, $"Aktarim: {license.InstallationId} -> {target.InstallationId}",
            ExpiresAtOverride: license.ExpiresAt, OriginLicenseId: origin), ct);

        license.TransferredToLicenseId = moved.Id;
        db.LicenseTransfers.Add(new LicenseTransfer
        {
            Id = EntityId.New(),
            OriginLicenseId = origin,
            FromLicenseId = license.Id,
            ToLicenseId = moved.Id,
            FromInstallationId = license.InstallationId,
            ToInstallationId = target.InstallationId,
            RequestedBy = requestedBy,
        });
        await notifier.TransferredAsync(customer, license, moved, requestedBy, ct);
        await db.SaveChangesAsync(ct);
        return new SelfServiceResult(moved, null);
    }

    /// <summary>Kurulum domainlerinden biri (alt alan iliskisi dahil) daha once bir deneme lisansinda yer aldi mi?</summary>
    private async Task<bool> DomainAlreadyTrialedAsync(IReadOnlyList<string> domains, CancellationToken ct)
    {
        var trialed = await db.IssuedLicenses.AsNoTracking()
            .Where(l => l.Type == LicenseTypes.Trial)
            .Select(l => l.AllowedDomains)
            .ToListAsync(ct);
        return trialed.SelectMany(d => d).Any(t => domains.Any(d => SelfServiceRules.DomainsRelated(t, d)));
    }
}
