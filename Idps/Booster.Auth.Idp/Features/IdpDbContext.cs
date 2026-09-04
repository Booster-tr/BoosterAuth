using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Idp.Features._base.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.Auth.Idp.Features;

public class IdpDbContext(DbContextOptions<IdpDbContext> options) : PostgresDbContext(options)
{
    public DbSet<Principal> Principals => Set<Principal>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<Session> Sessions => Set<Session>(); 
    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<PrincipalIdentity> PrincipalIdentities => Set<PrincipalIdentity>();
    public DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();
    public DbSet<PrincipalLink> PrincipalLinks => Set<PrincipalLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}