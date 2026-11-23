using System.Reflection;
using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Booster.Licensing.Portal.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
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

    /// <summary>
    /// Oturum cerezlerinin anahtarlari. Veritabaninda tutulur; aksi halde her yeniden baslatmada tum kullanicilarin
    /// oturumu duserdi.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
