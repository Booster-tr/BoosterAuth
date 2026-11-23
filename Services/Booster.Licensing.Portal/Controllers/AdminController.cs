using System.ComponentModel.DataAnnotations;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Portal.Services;
using Booster.Licensing.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Controllers;

/// <summary>Booster ekibinin ekranlari. Erisim: LicensingPortalOptions.AdminEmails.</summary>
[Authorize(Policy = PortalPolicies.Admin)]
[Route("admin")]
public sealed class AdminController(LicensingDbContext db, SigningKeyStore keys, LicenseIssuer issuer, OrderService orders) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.CustomerCount = await db.Customers.CountAsync(ct);
        ViewBag.LicenseCount = await db.IssuedLicenses.CountAsync(ct);
        return View(await keys.ListAsync(ct));
    }

    // ---------------------------------------------------------------- Musteriler

    [HttpGet("musteriler")]
    public async Task<IActionResult> Customers(CancellationToken ct) =>
        View(await db.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CustomerRow(c.Id, c.Name, c.ContactEmail, c.TaxNumber, c.Licenses.Count))
            .ToListAsync(ct));

    [HttpGet("musteriler/detay")]
    public async Task<IActionResult> CustomerDetail([FromQuery(Name = "CustomerId")] Guid customerId, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Installations).Include(x => x.Licenses)
            .SingleOrDefaultAsync(x => x.Id == customerId, ct);
        if (c is null) return NotFound();

        var licenseIds = c.Licenses.Select(l => l.Id).ToList();
        ViewBag.Trials = await db.TrialRequests.AsNoTracking().Where(t => t.CustomerId == c.Id)
            .OrderByDescending(t => t.CreatedTime).ToListAsync(ct);
        ViewBag.Transfers = await db.LicenseTransfers.AsNoTracking().Where(t => licenseIds.Contains(t.FromLicenseId))
            .OrderByDescending(t => t.CreatedTime).ToListAsync(ct);
        return View(c);
    }

    [HttpGet("musteriler/yeni")]
    public IActionResult NewCustomer() => View(new CustomerForm());

    [HttpPost("musteriler/yeni")]
    public async Task<IActionResult> NewCustomer(CustomerForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        db.Customers.Add(new Customer
        {
            Id = EntityId.New(),
            Name = form.Name!.Trim(),
            ContactEmail = form.ContactEmail!.Trim(),
            TaxOffice = Clean(form.TaxOffice),
            TaxNumber = Clean(form.TaxNumber),
            Address = Clean(form.Address),
        });
        await db.SaveChangesAsync(ct);
        return Redirect("/admin/musteriler");
    }

    // ---------------------------------------------------------------- Lisanslar

    [HttpGet("lisanslar")]
    public async Task<IActionResult> Licenses(CancellationToken ct) =>
        View(await db.IssuedLicenses.AsNoTracking().OrderByDescending(l => l.IssuedAt)
            .Select(l => new LicenseRow(l.Id, l.Customer.Name, l.InstallationId, l.Plan, l.Type, l.IssuedAt, l.ExpiresAt))
            .ToListAsync(ct));

    [HttpGet("lisanslar/yeni")]
    public async Task<IActionResult> NewLicense(Guid? customerId, CancellationToken ct)
    {
        await LoadCustomersAsync(ct);
        return View(LicenseForm.Paid(customerId));
    }

    [HttpPost("lisanslar/yeni")]
    public async Task<IActionResult> NewLicense(LicenseForm form, CancellationToken ct)
    {
        var (domains, domainErrors) = DomainInput.Parse(form.Domains);
        foreach (var e in domainErrors) ModelState.AddModelError(nameof(form.Domains), e);
        if (form.InstallationId is null || form.InstallationId == Guid.Empty)
            ModelState.AddModelError(nameof(form.InstallationId), "Gecerli bir kurulum GUID'i girin (urun panelinde Sistem > Lisans).");

        if (!ModelState.IsValid)
        {
            await LoadCustomersAsync(ct);
            return View(form);
        }

        var license = await issuer.IssueAsync(new IssueLicenseRequest(
            form.CustomerId!.Value, form.InstallationId!.Value, form.Plan!, form.Type!,
            form.MaxUsers, form.MaxPools, form.MaxClients, domains, form.DurationDays, form.GraceDays,
            User.FindFirst("email")?.Value ?? "admin", form.Note), ct);

        return Redirect($"/admin/lisanslar/detay?LicenseId={license.Id}");
    }

    [HttpGet("lisanslar/detay")]
    public async Task<IActionResult> LicenseDetail([FromQuery(Name = "LicenseId")] Guid licenseId, CancellationToken ct)
    {
        var license = await db.IssuedLicenses.AsNoTracking().Include(l => l.Customer)
            .SingleOrDefaultAsync(l => l.Id == licenseId, ct);
        return license is null ? NotFound() : View(license);
    }

    // ---------------------------------------------------------------- Siparisler

    [HttpGet("siparisler")]
    public async Task<IActionResult> Orders(OrderStatus? status, CancellationToken ct)
    {
        var q = db.Orders.AsNoTracking().Include(o => o.Customer).AsQueryable();
        if (status is not null) q = q.Where(o => o.Status == status);
        ViewBag.Status = status;
        return View(await q.OrderByDescending(o => o.CreatedTime).Take(500).ToListAsync(ct));
    }

    /// <summary>Odeme alindi: lisans otomatik uretilir. Ayni siparis ikinci kez onaylanamaz (cift lisans yok).</summary>
    [HttpPost("siparisler/odeme")]
    public async Task<IActionResult> ConfirmPayment([FromForm] Guid orderId, CancellationToken ct)
    {
        var license = await orders.ConfirmPaymentAsync(orderId, User.FindFirst("email")?.Value ?? "admin", ct);
        TempData[license is null ? "Error" : "Info"] = license is null
            ? "Sipariş zaten işlenmiş (onaylanmış ya da iptal edilmiş)."
            : "Ödeme onaylandı; lisans üretildi.";
        return Redirect("/admin/siparisler?status=Pending");
    }

    [HttpPost("siparisler/iptal")]
    public async Task<IActionResult> CancelOrder([FromForm] Guid orderId, CancellationToken ct)
    {
        var ok = await orders.CancelAsync(orderId, User.FindFirst("email")?.Value ?? "admin", ct);
        TempData[ok ? "Info" : "Error"] = ok ? "Sipariş iptal edildi." : "Yalnızca ödeme bekleyen sipariş iptal edilebilir.";
        return Redirect("/admin/siparisler?status=Pending");
    }

    // ---------------------------------------------------------------- Planlar

    [HttpGet("planlar")]
    public async Task<IActionResult> Plans(CancellationToken ct) =>
        View(await db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct));

    [HttpGet("planlar/duzenle")]
    public async Task<IActionResult> EditPlan([FromQuery(Name = "PlanId")] Guid? planId, CancellationToken ct)
    {
        if (planId is null) return View(new PlanForm { DurationMonths = 12, Currency = "TRY", IsActive = true });
        var p = await db.Plans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId, ct);
        if (p is null) return NotFound();
        return View(new PlanForm
        {
            Id = p.Id, Code = p.Code, Name = p.Name, MaxUsers = p.MaxUsers, MaxPools = p.MaxPools, MaxClients = p.MaxClients,
            DurationMonths = p.DurationMonths, Price = p.Price, Currency = p.Currency, IsActive = p.IsActive, SortOrder = p.SortOrder,
        });
    }

    /// <summary>Plan degisikligi verilmis siparisleri ETKILEMEZ (siparis plani kopyalar).</summary>
    [HttpPost("planlar/duzenle")]
    public async Task<IActionResult> EditPlan(PlanForm form, CancellationToken ct)
    {
        var code = form.Code?.Trim().ToLowerInvariant();
        if (code is not null && await db.Plans.AnyAsync(p => p.Code == code && p.Id != form.Id, ct))
            ModelState.AddModelError(nameof(form.Code), "Bu kod başka bir planda kullanılıyor.");
        if (!ModelState.IsValid) return View(form);

        var plan = form.Id is null ? null : await db.Plans.SingleOrDefaultAsync(p => p.Id == form.Id, ct);
        if (plan is null)
        {
            plan = new Plan { Id = EntityId.New() };
            db.Plans.Add(plan);
        }
        plan.Code = code!;
        plan.Name = form.Name!.Trim();
        plan.MaxUsers = form.MaxUsers;
        plan.MaxPools = form.MaxPools;
        plan.MaxClients = form.MaxClients;
        plan.DurationMonths = form.DurationMonths;
        plan.Price = form.Price;
        plan.Currency = form.Currency!.Trim().ToUpperInvariant();
        plan.IsActive = form.IsActive;
        plan.SortOrder = form.SortOrder;
        await db.SaveChangesAsync(ct);
        return Redirect("/admin/planlar");
    }

    private async Task LoadCustomersAsync(CancellationToken ct) =>
        ViewBag.Customers = await db.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CustomerOption(c.Id, c.Name)).ToListAsync(ct);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class PlanForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Kod zorunlu."), RegularExpression("^[a-zA-Z0-9-]{2,50}$", ErrorMessage = "Kod 2-50 karakter; harf, rakam, tire.")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Ad zorunlu."), StringLength(100)]
    public string? Name { get; set; }

    [Range(1, 10_000_000, ErrorMessage = "Kullanıcı limiti en az 1.")] public int MaxUsers { get; set; }
    [Range(1, 100_000, ErrorMessage = "Havuz limiti en az 1.")] public int MaxPools { get; set; }
    [Range(1, 100_000, ErrorMessage = "Uygulama limiti en az 1.")] public int MaxClients { get; set; }
    [Range(1, 60, ErrorMessage = "Süre 1-60 ay.")] public int DurationMonths { get; set; }
    [Range(0, 100_000_000, ErrorMessage = "Fiyat 0 veya fazla.")] public decimal Price { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Para birimi 3 harf (TRY, USD, EUR).")]
    public string? Currency { get; set; }

    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed record CustomerRow(Guid Id, string Name, string ContactEmail, string? TaxNumber, int LicenseCount);
public sealed record CustomerOption(Guid Id, string Name);
public sealed record LicenseRow(Guid Id, string Customer, Guid InstallationId, string Plan, string Type, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

public sealed class CustomerForm
{
    [Required(ErrorMessage = "Firma adi zorunlu."), StringLength(200)]
    public string? Name { get; set; }

    [Required(ErrorMessage = "E-posta zorunlu."), EmailAddress(ErrorMessage = "Gecerli bir e-posta girin."), StringLength(320)]
    public string? ContactEmail { get; set; }

    [StringLength(100)] public string? TaxOffice { get; set; }
    [StringLength(20)] public string? TaxNumber { get; set; }
    [StringLength(500)] public string? Address { get; set; }
}

public sealed class LicenseForm
{
    [Required(ErrorMessage = "Musteri secin.")]
    public Guid? CustomerId { get; set; }

    public Guid? InstallationId { get; set; }

    /// <summary>Virgul, bosluk veya satir ile ayrilmis domain'ler.</summary>
    public string? Domains { get; set; }

    [Required(ErrorMessage = "Plan adi zorunlu."), StringLength(50)]
    public string? Plan { get; set; }

    [Required, RegularExpression("^(trial|paid)$", ErrorMessage = "Tur trial veya paid olmali.")]
    public string? Type { get; set; }

    [Range(1, 10_000_000, ErrorMessage = "Kullanici limiti en az 1.")] public int MaxUsers { get; set; }
    [Range(1, 100_000, ErrorMessage = "Pool limiti en az 1.")] public int MaxPools { get; set; }
    [Range(1, 100_000, ErrorMessage = "Client limiti en az 1.")] public int MaxClients { get; set; }
    [Range(1, 3650, ErrorMessage = "Sure 1-3650 gun.")] public int DurationDays { get; set; }
    [Range(0, 365, ErrorMessage = "Ek sure 0-365 gun.")] public int GraceDays { get; set; }

    [StringLength(500)] public string? Note { get; set; }

    public static LicenseForm Paid(Guid? customerId) => new()
    {
        CustomerId = customerId,
        Plan = "standard",
        Type = LicenseTypes.Paid,
        MaxUsers = 1000,
        MaxPools = 5,
        MaxClients = 20,
        DurationDays = LicenseIssuer.Defaults.PaidDays,
        GraceDays = LicenseIssuer.Defaults.PaidGraceDays,
    };
}
