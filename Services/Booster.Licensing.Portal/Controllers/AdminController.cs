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
public sealed class AdminController(LicensingDbContext db, SigningKeyStore keys, LicenseIssuer issuer) : Controller
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

    private async Task LoadCustomersAsync(CancellationToken ct) =>
        ViewBag.Customers = await db.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CustomerOption(c.Id, c.Name)).ToListAsync(ct);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
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
