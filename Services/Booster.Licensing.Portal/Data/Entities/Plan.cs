using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Licensing.Portal.Data.Entities;

/// <summary>Satilan plan. Admin yonetir; musteri yalnizca aktif planlari gorur. Fiyatlar admin tarafindan girilir.</summary>
public class Plan : Entity
{
    /// <summary>Lisansa yazilan kisa ad (or. "pro").</summary>
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int MaxUsers { get; set; }
    public int MaxPools { get; set; }
    public int MaxClients { get; set; }
    public int DurationMonths { get; set; } = 12;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"DeletedTime\" IS NULL");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
    }
}

public enum OrderStatus
{
    /// <summary>Havale/EFT bekleniyor.</summary>
    Pending = 1,
    /// <summary>Odeme alindi; lisans uretildi.</summary>
    Paid = 2,
    Cancelled = 3,
}

/// <summary>
/// Siparis. Plan ve fatura bilgileri SIPARIS ANINDAKI halleriyle kopyalanir: plan ya da firma sonradan degisse de
/// siparisin tutari ve faturasi degismez.
/// </summary>
public class Order : Entity
{
    /// <summary>Havale aciklamasina yazilan tekil kod, or. BST-2026-000123.</summary>
    public string ReferenceCode { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public Guid InstallationId { get; set; }
    public List<string> Domains { get; set; } = [];

    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = default!;
    public string PlanName { get; set; } = default!;
    public int MaxUsers { get; set; }
    public int MaxPools { get; set; }
    public int MaxClients { get; set; }
    public int DurationMonths { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = default!;

    public string BillingName { get; set; } = default!;
    public string BillingEmail { get; set; } = default!;
    public string? BillingTaxOffice { get; set; }
    public string? BillingTaxNumber { get; set; }
    public string? BillingAddress { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string CreatedBy { get; set; } = default!;
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaidBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }

    /// <summary>Odeme onayinda uretilen lisans.</summary>
    public Guid? LicenseId { get; set; }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.ReferenceCode).IsUnique();
        builder.Property(x => x.PlanCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.BillingName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BillingEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.BillingTaxOffice).HasMaxLength(100);
        builder.Property(x => x.BillingTaxNumber).HasMaxLength(20);
        builder.Property(x => x.BillingAddress).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PaidBy).HasMaxLength(320);
        builder.Property(x => x.CancelledBy).HasMaxLength(320);
        builder.HasIndex(x => new { x.Status, x.CreatedTime });
        builder.HasIndex(x => x.CustomerId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
    }
}
