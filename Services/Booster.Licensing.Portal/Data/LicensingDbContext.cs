using Booster.Amqp.Outbox;
using System.Reflection;
using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Booster.Licensing.Portal.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Data;

public class LicensingDbContext(
    DbContextOptions<LicensingDbContext> options,
    IRequestContextAccessor requestContextAccessor)
    : PostgresDbContext(options, requestContextAccessor), IDataProtectionKeyContext
{
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<IssuedLicense> IssuedLicenses => Set<IssuedLicense>();
    public DbSet<CustomerInstallation> CustomerInstallations => Set<CustomerInstallation>();
    public DbSet<TrialRequest> TrialRequests => Set<TrialRequest>();
    public DbSet<LicenseTransfer> LicenseTransfers => Set<LicenseTransfer>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<LicenseReminder> LicenseReminders => Set<LicenseReminder>();

    /// <summary>Siparis referans kodu sayaci (BST-yyyy-NNNNNN). Veritabani dizisi: ayni anda iki siparis ayni kodu alamaz.</summary>
    public const string OrderReferenceSequence = "OrderReferenceNumbers";

    /// <summary>
    /// Oturum cerezlerinin anahtarlari. Veritabaninda tutulur; aksi halde her yeniden baslatmada tum kullanicilarin
    /// oturumu duserdi.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>(OrderReferenceSequence);

        // MassTransit tablolari (altyapi bekler) + Booster outbox (IEventBus olaylari; bkz. PortalNotifier).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddBoosterOutbox();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
