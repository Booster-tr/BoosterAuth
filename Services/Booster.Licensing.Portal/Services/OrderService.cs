using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Services;

/// <summary>
/// Siparis -&gt; havale/EFT -&gt; admin "odeme alindi" -&gt; lisans otomatik uretilir.
/// Yenileme ve plan yukseltme ayni akistir: kurulumda gecerli ucretli lisans varsa sure onun BITISINE eklenir.
/// </summary>
public sealed class OrderService(LicensingDbContext db, LicenseIssuer issuer, TimeProvider time, PortalNotifier notifier)
{
    public async Task<Order> CreateAsync(Customer customer, CustomerInstallation installation, Plan plan, string createdBy, CancellationToken ct)
    {
        if (!plan.IsActive) throw new InvalidOperationException("Pasif plan icin siparis verilemez.");

        var number = await db.Database
            .SqlQueryRaw<long>($"SELECT nextval('\"{LicensingDbContext.OrderReferenceSequence}\"') AS \"Value\"")
            .SingleAsync(ct);

        var order = new Order
        {
            Id = EntityId.New(),
            ReferenceCode = $"BST-{time.GetUtcNow().Year}-{number:000000}",
            CustomerId = customer.Id,
            InstallationId = installation.InstallationId,
            Domains = [.. installation.Domains],
            PlanId = plan.Id,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            MaxUsers = plan.MaxUsers,
            MaxPools = plan.MaxPools,
            MaxClients = plan.MaxClients,
            DurationMonths = plan.DurationMonths,
            Price = plan.Price,
            Currency = plan.Currency,
            BillingName = customer.Name,
            BillingEmail = customer.ContactEmail,
            BillingTaxOffice = customer.TaxOffice,
            BillingTaxNumber = customer.TaxNumber,
            BillingAddress = customer.Address,
            CreatedBy = createdBy,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await notifier.OrderReceivedAsync(order, ct);
        return order;
    }

    /// <summary>
    /// Odeme alindi: lisans uretilir ve siparise baglanir. Siparis satiri kosullu guncellemeyle (yalnizca Pending iken)
    /// kilitlenir; cift tiklama ya da iki admin ayni anda onaylasa da TEK lisans uretilir. Hata olursa hepsi geri alinir.
    /// </summary>
    /// <returns>Uretilen lisans; siparis zaten islenmisse null.</returns>
    public async Task<IssuedLicense?> ConfirmPaymentAsync(Guid orderId, string paidBy, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = time.GetUtcNow();

        var claimed = await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.PaidAt, now)
                .SetProperty(o => o.PaidBy, paidBy), ct);
        if (claimed == 0) return null;

        var order = await db.Orders.SingleAsync(o => o.Id == orderId, ct);

        // Yenileme: ayni kurulumda gecerli ve aktarilmamis ucretli lisans varsa yeni sure onun bitisinden baslar
        // (erken yenileyen musterinin kalan gunleri yanmaz).
        var current = await db.IssuedLicenses.AsNoTracking()
            .Where(l => l.InstallationId == order.InstallationId && l.Type == LicenseTypes.Paid
                        && l.TransferredToLicenseId == null && l.ExpiresAt > now)
            .OrderByDescending(l => l.ExpiresAt)
            .FirstOrDefaultAsync(ct);
        var start = current?.ExpiresAt ?? now;

        var license = await issuer.IssueAsync(new IssueLicenseRequest(
            order.CustomerId, order.InstallationId, order.PlanCode, LicenseTypes.Paid,
            order.MaxUsers, order.MaxPools, order.MaxClients, order.Domains,
            1, LicenseIssuer.Defaults.PaidGraceDays, paidBy, $"Siparis {order.ReferenceCode}",
            ExpiresAtOverride: start.AddMonths(order.DurationMonths)), ct);

        order.LicenseId = license.Id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // E-posta COMMIT'ten sonra: onay geri alinirsa e-posta gitmez.
        await notifier.LicenseIssuedAsync(order, license, ct);
        return license;
    }

    /// <summary>Yalnizca odeme bekleyen siparis iptal edilebilir.</summary>
    public async Task<bool> CancelAsync(Guid orderId, string cancelledBy, CancellationToken ct) =>
        await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Cancelled)
                .SetProperty(o => o.CancelledAt, time.GetUtcNow())
                .SetProperty(o => o.CancelledBy, cancelledBy), ct) > 0;

    /// <summary>
    /// Ilk acilista plan yoksa ornek planlar eklenir. Fiyatlar 0 (girilmedi); admin Planlar ekranindan duzenler.
    /// </summary>
    public async Task SeedPlansAsync(CancellationToken ct)
    {
        if (await db.Plans.AnyAsync(ct)) return;
        db.Plans.AddRange(
            new Plan { Id = EntityId.New(), Code = "baslangic", Name = "Başlangıç", MaxUsers = 1000, MaxPools = 2, MaxClients = 10, SortOrder = 1 },
            new Plan { Id = EntityId.New(), Code = "profesyonel", Name = "Profesyonel", MaxUsers = 10000, MaxPools = 10, MaxClients = 50, SortOrder = 2 },
            new Plan { Id = EntityId.New(), Code = "kurumsal", Name = "Kurumsal", MaxUsers = 100000, MaxPools = 100, MaxClients = 500, SortOrder = 3 });
        await db.SaveChangesAsync(ct);
    }
}
