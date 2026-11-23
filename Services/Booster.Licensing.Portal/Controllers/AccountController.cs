using System.ComponentModel.DataAnnotations;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Controllers;

/// <summary>
/// Musterinin self-servis alani: firma, kurulumlar, ucretsiz deneme, lisanslar, aktarim.
/// Giris Booster Auth (musteri havuzu) ile; e-postasi DOGRULANMIS olmalidir (deneme kurallari e-postaya dayanir).
/// Her kayit giris yapan kullanicinin firmasina baglidir; baska firmanin kaydi goruntulenemez.
/// </summary>
[Authorize(Policy = PortalPolicies.Customer)]
[Route("hesap")]
public sealed class AccountController(LicensingDbContext db, CustomerPortalService portal, OrderService orders,
    Booster.Licensing.Portal.Options.LicensingPortalOptions options) : Controller
{
    private Guid PrincipalId => Guid.Parse(User.FindFirst("sub")!.Value);
    private string Email => User.FindFirst("email")!.Value;

    /// <summary>Cloudflare tunnel arkasinda gercek istemci IP'si CF-Connecting-IP basligindadir.</summary>
    private string? ClientIp =>
        Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();

    private Task<Customer?> CurrentCustomerAsync(CancellationToken ct) =>
        db.Customers.Include(c => c.Installations).SingleOrDefaultAsync(c => c.OwnerPrincipalId == PrincipalId, ct);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        await CurrentCustomerAsync(ct) is null ? Redirect("/hesap/firma") : Redirect("/hesap/kurulumlar");

    /// <summary>Deneme (Paketler sayfasindaki "Ucretsiz dene") icin ozel plan kodu.</summary>
    public const string TrialPlan = "deneme";

    /// <summary>
    /// Sitedeki "Lisans al" / "Ucretsiz dene" butonlari buraya gelir (giris zorunlu; Booster Auth'a yonlenir).
    /// Eksik adimi sirayla tamamlatir, her adimdan sonra buraya doner: firma -> kurulum -> (kurulum secimi) ->
    /// secilen planin siparis ekrani. Deneme icin son adim Kurulumlarim (deneme butonu oradadir).
    /// </summary>
    [HttpGet("satin-al")]
    public async Task<IActionResult> Buy(string? plan, Guid? installationId, CancellationToken ct)
    {
        plan = string.IsNullOrWhiteSpace(plan) ? null : plan.Trim().ToLowerInvariant();
        var self = $"/hesap/satin-al?plan={Uri.EscapeDataString(plan ?? "")}";
        var c = await CurrentCustomerAsync(ct);
        if (c is null) return Redirect($"/hesap/firma?returnUrl={Uri.EscapeDataString(self)}");
        if (c.Installations.Count == 0) return Redirect($"/hesap/kurulumlar/yeni?returnUrl={Uri.EscapeDataString(self)}");
        if (plan is null || plan == TrialPlan) return Redirect("/hesap/kurulumlar");

        var inst = installationId is { } g ? c.Installations.SingleOrDefault(i => i.InstallationId == g) : null;
        if (inst is null && c.Installations.Count == 1) inst = c.Installations.Single();
        if (inst is null)
        {
            ViewBag.Plan = plan;
            return View("ChooseInstallation", c.Installations.OrderBy(i => i.Label).ToList());
        }
        return Redirect($"/hesap/siparis/yeni?InstallationId={inst.InstallationId}&plan={Uri.EscapeDataString(plan)}");
    }

    // ---------------------------------------------------------------- Firma

    [HttpGet("firma")]
    public async Task<IActionResult> Company(string? returnUrl, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(c is null
            ? new CompanyForm { ContactEmail = Email }
            : new CompanyForm { Name = c.Name, ContactEmail = c.ContactEmail, TaxOffice = c.TaxOffice, TaxNumber = c.TaxNumber, Address = c.Address });
    }

    [HttpPost("firma")]
    public async Task<IActionResult> Company(CompanyForm form, string? returnUrl, CancellationToken ct)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid) return View(form);

        var c = await CurrentCustomerAsync(ct);
        if (c is null)
        {
            c = new Customer { Id = EntityId.New(), OwnerPrincipalId = PrincipalId };
            db.Customers.Add(c);
        }
        c.Name = form.Name!.Trim();
        c.ContactEmail = form.ContactEmail!.Trim();
        c.TaxOffice = Clean(form.TaxOffice);
        c.TaxNumber = Clean(form.TaxNumber);
        c.Address = Clean(form.Address);
        await db.SaveChangesAsync(ct);

        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/hesap/kurulumlar");
    }

    // ---------------------------------------------------------------- Kurulumlar

    [HttpGet("kurulumlar")]
    public async Task<IActionResult> Installations(CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        if (c is null) return Redirect("/hesap/firma");

        var licenses = await db.IssuedLicenses.AsNoTracking().Where(l => l.CustomerId == c.Id)
            .OrderByDescending(l => l.IssuedAt).ToListAsync(ct);
        ViewBag.Licenses = licenses;
        ViewBag.Customer = c;
        return View(c.Installations.OrderBy(i => i.Label).ToList());
    }

    [HttpGet("kurulumlar/yeni")]
    public async Task<IActionResult> NewInstallation(Guid? installationId, string? returnUrl, CancellationToken ct)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        var c = await CurrentCustomerAsync(ct);
        if (c is null)
        {
            var back = $"/hesap/kurulumlar/yeni?installationId={installationId}" + (returnUrl is null ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}");
            return Redirect($"/hesap/firma?returnUrl={Uri.EscapeDataString(back)}");
        }
        // Paneldeki "Lisans al" butonu zaten kayitli bir kurulumla gelirse formu tekrar gosterme.
        if (installationId is { } g && c.Installations.Any(i => i.InstallationId == g))
            return Redirect(returnUrl ?? "/hesap/kurulumlar");
        ViewBag.ReturnUrl = returnUrl;
        return View(new InstallationForm { InstallationId = installationId });
    }

    [HttpPost("kurulumlar/yeni")]
    public async Task<IActionResult> NewInstallation(InstallationForm form, string? returnUrl, CancellationToken ct)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewBag.ReturnUrl = returnUrl;
        var c = await CurrentCustomerAsync(ct);
        if (c is null) return Redirect("/hesap/firma");

        var (domains, errors) = DomainInput.Parse(form.Domains);
        foreach (var e in errors) ModelState.AddModelError(nameof(form.Domains), e);
        if (form.InstallationId is null || form.InstallationId == Guid.Empty)
            ModelState.AddModelError(nameof(form.InstallationId), "Geçerli bir kurulum kimliği (GUID) girin. Ürün panelinde Sistem > Lisans ekranında bulunur.");
        else if (await db.CustomerInstallations.AnyAsync(i => i.InstallationId == form.InstallationId, ct))
            // Baska firmanin kurulumuna lisans alinamasin diye ayni GUID ikinci kez kaydedilemez (kime ait oldugu soylenmez).
            ModelState.AddModelError(nameof(form.InstallationId), "Bu kurulum kimliği zaten kayıtlı. Size aitse ve göremiyorsanız bizimle iletişime geçin.");

        if (!ModelState.IsValid) return View(form);

        db.CustomerInstallations.Add(new CustomerInstallation
        {
            Id = EntityId.New(),
            CustomerId = c.Id,
            InstallationId = form.InstallationId!.Value,
            Label = form.Label!.Trim(),
            Domains = domains,
        });
        await db.SaveChangesAsync(ct);
        return Redirect(returnUrl ?? "/hesap/kurulumlar");
    }

    // ---------------------------------------------------------------- Deneme

    [HttpPost("deneme")]
    public async Task<IActionResult> Trial([FromForm] Guid installationId, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var inst = c?.Installations.SingleOrDefault(i => i.InstallationId == installationId);
        if (c is null || inst is null) return NotFound();

        var result = await portal.RequestTrialAsync(c, inst, Email, ClientIp, ct);
        if (result.Error is not null)
        {
            TempData["Error"] = SelfServiceErrors.Text(result.Error);
            TempData["ErrorCode"] = result.Error;
            return Redirect("/hesap/kurulumlar");
        }
        return Redirect($"/hesap/lisans?LicenseId={result.License!.Id}");
    }

    // ---------------------------------------------------------------- Lisanslar

    [HttpGet("lisans")]
    public async Task<IActionResult> License([FromQuery(Name = "LicenseId")] Guid licenseId, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var license = c is null ? null : await db.IssuedLicenses.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == licenseId && l.CustomerId == c.Id, ct);
        if (license is null) return NotFound();

        ViewBag.Installation = c!.Installations.SingleOrDefault(i => i.InstallationId == license.InstallationId);
        ViewBag.Transfers = await db.LicenseTransfers.AsNoTracking()
            .Where(t => t.OriginLicenseId == (license.OriginLicenseId ?? license.Id)).OrderBy(t => t.CreatedTime).ToListAsync(ct);
        return View(license);
    }

    [HttpGet("lisans/aktar")]
    public async Task<IActionResult> Transfer([FromQuery(Name = "LicenseId")] Guid licenseId, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var license = c is null ? null : await db.IssuedLicenses.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == licenseId && l.CustomerId == c.Id, ct);
        if (license is null) return NotFound();

        ViewBag.License = license;
        ViewBag.Targets = c!.Installations.Where(i => i.InstallationId != license.InstallationId).OrderBy(i => i.Label).ToList();
        return View();
    }

    [HttpPost("lisans/aktar")]
    public async Task<IActionResult> Transfer([FromQuery(Name = "LicenseId")] Guid licenseId, [FromForm] Guid targetInstallationId, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var target = c?.Installations.SingleOrDefault(i => i.InstallationId == targetInstallationId);
        if (c is null || target is null) return NotFound();

        var result = await portal.TransferAsync(c, licenseId, target, Email, ct);
        if (result.Error is not null)
        {
            TempData["Error"] = SelfServiceErrors.Text(result.Error);
            TempData["ErrorCode"] = result.Error;
            return Redirect($"/hesap/lisans?LicenseId={licenseId}");
        }
        return Redirect($"/hesap/lisans?LicenseId={result.License!.Id}");
    }

    // ---------------------------------------------------------------- Siparis (satin alma / yenileme / yukseltme)

    [HttpGet("siparis/yeni")]
    public async Task<IActionResult> NewOrder([FromQuery(Name = "InstallationId")] Guid installationId, string? plan, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var inst = c?.Installations.SingleOrDefault(i => i.InstallationId == installationId);
        if (c is null || inst is null) return NotFound();

        ViewBag.Installation = inst;
        // Paketler sayfasinda secilen plan (vurgulanir).
        ViewBag.SelectedPlan = plan?.Trim().ToLowerInvariant();
        ViewBag.Current = await db.IssuedLicenses.AsNoTracking()
            .Where(l => l.InstallationId == installationId && l.Type == "paid" && l.TransferredToLicenseId == null && l.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(l => l.ExpiresAt).FirstOrDefaultAsync(ct);
        return View(await db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(ct));
    }

    [HttpPost("siparis/yeni")]
    public async Task<IActionResult> NewOrder([FromForm] Guid installationId, [FromForm] Guid planId, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var inst = c?.Installations.SingleOrDefault(i => i.InstallationId == installationId);
        // Pasif plan siparis edilemez (adres elle yazilsa da).
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == planId && p.IsActive, ct);
        if (c is null || inst is null || plan is null) return NotFound();

        var order = await orders.CreateAsync(c, inst, plan, Email, ct);
        return Redirect($"/hesap/siparis?OrderId={order.Id}");
    }

    [HttpGet("siparis")]
    public async Task<IActionResult> Order([FromQuery(Name = "OrderId")] Guid orderId, CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        var order = c is null ? null : await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == c.Id, ct);
        if (order is null) return NotFound();
        ViewBag.Bank = options.Bank;
        return View(order);
    }

    [HttpGet("siparisler")]
    public async Task<IActionResult> Orders(CancellationToken ct)
    {
        var c = await CurrentCustomerAsync(ct);
        if (c is null) return Redirect("/hesap/firma");
        return View(await db.Orders.AsNoTracking().Where(o => o.CustomerId == c.Id).OrderByDescending(o => o.CreatedTime).ToListAsync(ct));
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class CompanyForm
{
    [Required(ErrorMessage = "Firma adı zorunlu."), StringLength(200)]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Fatura e-postası zorunlu."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(320)]
    public string? ContactEmail { get; set; }

    [StringLength(100)] public string? TaxOffice { get; set; }
    [StringLength(20)] public string? TaxNumber { get; set; }
    [StringLength(500)] public string? Address { get; set; }
}

public sealed class InstallationForm
{
    public Guid? InstallationId { get; set; }

    [Required(ErrorMessage = "Kurulum için bir ad girin (ör. Üretim)."), StringLength(100)]
    public string? Label { get; set; }

    public string? Domains { get; set; }
}
